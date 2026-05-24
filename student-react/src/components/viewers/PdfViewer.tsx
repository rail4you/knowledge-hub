import { useEffect, useRef, useState, useCallback } from 'react';
import { ChevronLeft, ChevronRight, ZoomIn, ZoomOut, Maximize } from 'lucide-react';

interface PdfViewerProps {
  data: ArrayBuffer;
  fileName: string;
}

export function PdfViewer({ data }: PdfViewerProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const pdfDocRef = useRef<any>(null);
  const renderSeqRef = useRef(0); // sequence counter to handle rapid navigation
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(0);
  const [scale, setScale] = useState(1);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const dataRef = useRef(data);
  dataRef.current = data;

  const renderPage = useCallback(async (pageNum: number, currentScale: number, seq: number) => {
    if (!pdfDocRef.current) return;
    try {
      const page = await pdfDocRef.current.getPage(pageNum);
      // Check if a newer render was requested while we were getting the page
      if (seq !== renderSeqRef.current) return;
      const viewport = page.getViewport({ scale: currentScale * 1.5 });
      const canvas = canvasRef.current;
      if (!canvas) return;
      const context = canvas.getContext('2d')!;
      canvas.height = viewport.height;
      canvas.width = viewport.width;
      await page.render({ canvasContext: context, viewport }).promise;
    } catch (e) {
      console.error('Render page error:', e);
    }
  }, []);

  // Load PDF
  useEffect(() => {
    let cancelled = false;
    const loadPdf = async () => {
      try {
        setIsLoading(true);
        setError('');
        const pdfjsLib = await import('pdfjs-dist');
        pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
          'pdfjs-dist/build/pdf.worker.min.mjs',
          import.meta.url
        ).toString();

        const loadingTask = pdfjsLib.getDocument({
          data: new Uint8Array(dataRef.current),
          cMapUrl: 'https://unpkg.com/pdfjs-dist@5.7.284/cmaps/',
          cMapPacked: true,
        });

        const pdf = await loadingTask.promise;
        if (cancelled) return;
        pdfDocRef.current = pdf;
        setTotalPages(pdf.numPages);
        setCurrentPage(1);
        const seq = ++renderSeqRef.current;
        await renderPage(1, 1, seq);
      } catch (e: any) {
        if (cancelled) return;
        console.error('PDF load error:', e);
        setError(e.message || 'PDF 加载失败');
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };
    loadPdf();
    return () => { cancelled = true; };
  }, [renderPage]);

  // Re-render on page/scale change — increment sequence so stale renders are discarded
  useEffect(() => {
    if (!pdfDocRef.current || isLoading) return;
    const seq = ++renderSeqRef.current;
    renderPage(currentPage, scale, seq);
  }, [currentPage, scale, renderPage, isLoading]);

  const prevPage = () => {
    if (currentPage <= 1) return;
    setCurrentPage(currentPage - 1);
  };

  const nextPage = () => {
    if (currentPage >= totalPages) return;
    setCurrentPage(currentPage + 1);
  };

  const zoomIn = () => {
    if (scale >= 3) return;
    setScale(s => Math.min(3, s + 0.25));
  };

  const zoomOut = () => {
    if (scale <= 0.5) return;
    setScale(s => Math.max(0.5, s - 0.25));
  };

  const fitWidth = () => {
    setScale(1);
  };

  return (
    <div className="file-viewer-container">
      {/* Toolbar */}
      <div className="file-viewer-toolbar">
        <div className="flex items-center gap-2">
          <button className="viewer-toolbar-btn" onClick={prevPage} disabled={currentPage <= 1}>
            <ChevronLeft className="h-4 w-4" />
          </button>
          <span className="text-sm text-secondary min-w-[72px] text-center">
            {currentPage} / {totalPages}
          </span>
          <button className="viewer-toolbar-btn" onClick={nextPage} disabled={currentPage >= totalPages}>
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>
        <div className="flex items-center gap-2">
          <button className="viewer-toolbar-btn" onClick={zoomOut} disabled={scale <= 0.5}>
            <ZoomOut className="h-4 w-4" />
          </button>
          <span className="text-sm text-secondary min-w-[48px] text-center">{Math.round(scale * 100)}%</span>
          <button className="viewer-toolbar-btn" onClick={zoomIn} disabled={scale >= 3}>
            <ZoomIn className="h-4 w-4" />
          </button>
          <button className="viewer-toolbar-btn" onClick={fitWidth}>
            <Maximize className="h-4 w-4" />
          </button>
        </div>
      </div>

      {/* Canvas */}
      <div className="file-viewer-canvas-container">
        {isLoading && (
          <div className="file-viewer-loading">
            <div className="loading-spinner" />
            <p className="mt-4 text-secondary text-sm">正在加载 PDF…</p>
          </div>
        )}
        {error && <div className="file-viewer-error">{error}</div>}
        <canvas
          ref={canvasRef}
          className="file-viewer-canvas"
          style={{ display: isLoading || error ? 'none' : 'block' }}
        />
      </div>
    </div>
  );
}
