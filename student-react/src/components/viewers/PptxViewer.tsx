import { useEffect, useRef, useState, useCallback, useMemo } from 'react';
import { ChevronLeft, ChevronRight, ZoomIn, ZoomOut } from 'lucide-react';
import { PPTXViewer as PPTXViewerLib } from 'pptxviewjs';

interface PptxViewerProps {
  data: ArrayBuffer;
  fileName: string;
}

export function PptxViewer({ data }: PptxViewerProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const viewerRef = useRef<PPTXViewerLib | null>(null);
  const renderTokenRef = useRef(0);

  const [slideCount, setSlideCount] = useState(0);
  const [currentSlide, setCurrentSlide] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [isRendering, setIsRendering] = useState(false);
  const [error, setError] = useState('');
  const [zoom, setZoom] = useState(1);

  const slideLabel = `${currentSlide + 1} / ${slideCount || 0}`;
  const zoomLabel = `${Math.round(zoom * 100)}%`;

  const canGoPrev = currentSlide > 0;
  const canGoNext = currentSlide < slideCount - 1;

  const visiblePageIndexes = useMemo(() => {
    const total = slideCount;
    const current = currentSlide;
    const maxVisible = 10;

    if (total <= maxVisible) {
      return Array.from({ length: total }, (_, i) => i);
    }

    const half = Math.floor(maxVisible / 2);
    let start = Math.max(0, current - half);
    let end = Math.min(total - 1, start + maxVisible - 1);

    if (end - start < maxVisible - 1) {
      start = Math.max(0, end - maxVisible + 1);
    }

    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  }, [slideCount, currentSlide]);

  const hasLeftEllipsis = visiblePageIndexes[0] > 0;
  const hasRightEllipsis =
    visiblePageIndexes.length > 0 && visiblePageIndexes[visiblePageIndexes.length - 1] < slideCount - 1;

  const renderSlide = useCallback(async (index: number, currentZoom: number, token: number) => {
    if (!viewerRef.current) return;
    setIsRendering(true);
    try {
      const canvas = canvasRef.current;
      if (!canvas) return;
      await viewerRef.current.render(canvas, {
        slideIndex: index,
        scale: currentZoom,
        quality: 'high',
      });
      if (token !== renderTokenRef.current) return;
      setCurrentSlide(viewerRef.current.getCurrentSlideIndex());
      setError('');
    } catch (e: unknown) {
      if (token !== renderTokenRef.current) return;
      console.error('PPTX slide render error:', e);
      setError(e instanceof Error ? e.message : 'PPTX 页面渲染失败');
    } finally {
      if (token === renderTokenRef.current) {
        setIsRendering(false);
      }
    }
  }, []);

  // Load presentation
  useEffect(() => {
    const token = ++renderTokenRef.current;

    // Dispose previous viewer
    if (viewerRef.current) {
      viewerRef.current.destroy();
      viewerRef.current = null;
    }

    const canvas = canvasRef.current;
    if (!canvas || !data.byteLength) return;

    setIsLoading(true);
    setIsRendering(false);
    setError('');
    setSlideCount(0);
    setCurrentSlide(0);
    setZoom(1);

    const loadPresentation = async () => {
      try {
        const viewer = new PPTXViewerLib({
          canvas,
          debug: false,
          slideSizeMode: 'fit',
          backgroundColor: '#ffffff',
          autoRenderFirstSlide: false,
        });

        await viewer.loadFile(data.slice(0));

        if (token !== renderTokenRef.current) {
          viewer.destroy();
          return;
        }

        viewerRef.current = viewer;
        setSlideCount(viewer.getSlideCount());
        await renderSlide(0, 1, token);
      } catch (e: unknown) {
        if (token !== renderTokenRef.current) return;
        console.error('PPTX render error:', e);
        setError(e instanceof Error ? e.message : 'PPTX 预览加载失败');
      } finally {
        if (token === renderTokenRef.current) {
          setIsLoading(false);
        }
      }
    };

    loadPresentation();

    return () => {
      renderTokenRef.current++;
      if (viewerRef.current) {
        viewerRef.current.destroy();
        viewerRef.current = null;
      }
    };
  }, [data, renderSlide]);

  const handlePrevSlide = async () => {
    if (!canGoPrev) return;
    const token = renderTokenRef.current;
    await renderSlide(currentSlide - 1, zoom, token);
  };

  const handleNextSlide = async () => {
    if (!canGoNext) return;
    const token = renderTokenRef.current;
    await renderSlide(currentSlide + 1, zoom, token);
  };

  const handleGoToSlide = async (index: number) => {
    if (index < 0 || index >= slideCount) return;
    const token = renderTokenRef.current;
    await renderSlide(index, zoom, token);
  };

  const handleZoomIn = async () => {
    if (zoom >= 2) return;
    const newZoom = Math.min(2, zoom + 0.25);
    setZoom(newZoom);
    await renderSlide(currentSlide, newZoom, renderTokenRef.current);
  };

  const handleZoomOut = async () => {
    if (zoom <= 0.5) return;
    const newZoom = Math.max(0.5, zoom - 0.25);
    setZoom(newZoom);
    await renderSlide(currentSlide, newZoom, renderTokenRef.current);
  };

  const handleResetZoom = async () => {
    if (zoom === 1) return;
    setZoom(1);
    await renderSlide(currentSlide, 1, renderTokenRef.current);
  };

  return (
    <div className="pptx-viewer-container">
      {error ? (
        <div className="file-viewer-error">{error}</div>
      ) : (
        <>
          {/* Toolbar */}
          <div className="file-viewer-toolbar">
            <div className="flex items-center gap-2">
              <button
                className="viewer-toolbar-btn"
                onClick={handlePrevSlide}
                disabled={!canGoPrev || isLoading || isRendering}
              >
                <ChevronLeft className="h-4 w-4" />
              </button>
              <span className="text-sm text-secondary min-w-[72px] text-center font-semibold">{slideLabel}</span>
              <button
                className="viewer-toolbar-btn"
                onClick={handleNextSlide}
                disabled={!canGoNext || isLoading || isRendering}
              >
                <ChevronRight className="h-4 w-4" />
              </button>
            </div>
            <div className="flex items-center gap-2">
              <button className="viewer-toolbar-btn" onClick={handleZoomOut} disabled={zoom <= 0.5 || isLoading || isRendering}>
                <ZoomOut className="h-4 w-4" />
              </button>
              <button className="viewer-toolbar-btn" onClick={handleResetZoom} disabled={zoom === 1 || isLoading || isRendering}>
                <span className="text-xs font-medium">{zoomLabel}</span>
              </button>
              <button className="viewer-toolbar-btn" onClick={handleZoomIn} disabled={zoom >= 2 || isLoading || isRendering}>
                <ZoomIn className="h-4 w-4" />
              </button>
            </div>
          </div>

          {/* Slide stage */}
          <div className="pptx-slide-stage">
            <canvas
              ref={canvasRef}
              className="pptx-slide-canvas"
              style={{ visibility: isLoading ? 'hidden' : 'visible' }}
            />
            {isLoading && (
              <div className="file-viewer-loading">
                <div className="loading-spinner" />
                <p className="mt-4 text-secondary text-sm">正在加载 PPTX…</p>
              </div>
            )}
            {isRendering && !isLoading && (
              <div className="pptx-rendering-mask">正在渲染幻灯片…</div>
            )}
          </div>

          {/* Page strip */}
          {slideCount > 1 && (
            <div className="pptx-page-strip">
              {hasLeftEllipsis && (
                <>
                  <button className="pptx-page-pill" onClick={() => handleGoToSlide(0)}>1</button>
                  <span className="pptx-page-ellipsis">...</span>
                </>
              )}
              {visiblePageIndexes.map(idx => (
                <button
                  key={idx}
                  className={`pptx-page-pill ${idx === currentSlide ? 'active' : ''}`}
                  onClick={() => handleGoToSlide(idx)}
                >
                  {idx + 1}
                </button>
              ))}
              {hasRightEllipsis && (
                <>
                  <span className="pptx-page-ellipsis">...</span>
                  <button className="pptx-page-pill" onClick={() => handleGoToSlide(slideCount - 1)}>
                    {slideCount}
                  </button>
                </>
              )}
            </div>
          )}
        </>
      )}
    </div>
  );
}
