import { useState, useEffect, useCallback } from 'react';
import { X, Download, Loader2, FileQuestion } from 'lucide-react';
import { fetchResourcePreview, downloadResourceFile, getFileType, formatFileSize, FileType } from '../lib/file-api';
import { PdfViewer } from './viewers/PdfViewer';
import { WordViewer } from './viewers/WordViewer';
import { PptxViewer } from './viewers/PptxViewer';

/** Simple toast notification */
function showToast(msg: string, type: 'success' | 'error' = 'success') {
  const el = document.createElement('div');
  el.textContent = msg;
  Object.assign(el.style, {
    position: 'fixed',
    top: '24px',
    left: '50%',
    transform: 'translateX(-50%)',
    padding: '10px 24px',
    borderRadius: '8px',
    fontSize: '14px',
    color: '#fff',
    background: type === 'error' ? '#ef4444' : '#22c55e',
    zIndex: '2000',
    boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
    transition: 'opacity 0.3s',
  } as React.CSSProperties);
  document.body.appendChild(el);
  setTimeout(() => { el.style.opacity = '0'; }, 2000);
  setTimeout(() => { document.body.removeChild(el); }, 2500);
}

interface FilePreviewProps {
  resourceId: string | null;
  resourceName: string;
  fileExtension: string;
  fileSize: number;
  onClose: () => void;
}

export function FilePreviewModal({
  resourceId,
  resourceName,
  fileExtension,
  fileSize,
  onClose,
}: FilePreviewProps) {
  const [fileData, setFileData] = useState<ArrayBuffer | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  const fileType = getFileType(fileExtension);
  const fileName = resourceName || `file.${fileExtension.replace('.', '')}`;

  const loadFile = useCallback(async () => {
    if (!resourceId) return;
    setIsLoading(true);
    setError('');
    try {
      const data = await fetchResourcePreview(resourceId);
      setFileData(data);
    } catch (e: any) {
      console.error('File load error:', e);
      setError('预览加载失败，请稍后重试');
    } finally {
      setIsLoading(false);
    }
  }, [resourceId]);

  useEffect(() => {
    if (resourceId) {
      loadFile();
    }
    return () => {
      setFileData(null);
    };
  }, [resourceId, loadFile]);

  // Close on Escape
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  const handleDownload = async () => {
    if (!resourceId) return;
    try {
      await downloadResourceFile(resourceId, fileName);
      showToast('下载已开始');
    } catch (e) {
      console.error('Download error:', e);
      showToast('下载失败，请重试', 'error');
    }
  };

  const renderContent = () => {
    if (isLoading) {
      return (
        <div className="file-viewer-loading">
          <div className="loading-spinner" />
          <p className="mt-4 text-secondary text-sm">正在加载预览…</p>
        </div>
      );
    }

    if (error) {
      return <div className="file-viewer-error">{error}</div>;
    }

    if (!fileData || fileData.byteLength === 0) {
      return null;
    }

    switch (fileType) {
      case 'pdf':
        return <PdfViewer data={fileData} fileName={fileName} />;
      case 'word':
        return <WordViewer data={fileData} fileName={fileName} />;
      case 'pptx':
        return <PptxViewer data={fileData} fileName={fileName} />;
      case 'excel':
        return <UnsupportedPreview fileName={fileName} message="暂不支持 Excel 文件预览，请下载后查看" />;
      case 'image':
        return <ImagePreview data={fileData} fileName={fileName} />;
      case 'text':
        return <TextPreview data={fileData} />;
      default:
        return <UnsupportedPreview fileName={fileName} />;
    }
  };

  if (!resourceId) return null;

  return (
    <div className="file-preview-overlay" onClick={onClose}>
      <div
        className="file-preview-modal"
        onClick={e => e.stopPropagation()}
        style={{ maxWidth: fileType === 'pptx' ? 1320 : 1040 }}
      >
        {/* Header */}
        <div className="file-preview-header">
          <h3 className="file-preview-title">{fileName}</h3>
          <div className="flex items-center gap-3">
            <button className="btn btn-primary btn-sm" onClick={handleDownload}>
              <Download className="h-4 w-4" /> 下载
            </button>
            <button className="file-preview-close" onClick={onClose}>
              <X className="h-5 w-5" />
            </button>
          </div>
        </div>

        {/* Content */}
        <div className="file-preview-body">
          {renderContent()}
        </div>

        {/* Footer */}
        <div className="file-preview-footer">
          <span className="text-secondary text-sm">
            {fileName} ({formatFileSize(fileSize)})
          </span>
        </div>
      </div>
    </div>
  );
}

function UnsupportedPreview({ fileName, message }: { fileName: string; message?: string }) {
  return (
    <div className="file-viewer-unsupported">
      <FileQuestion className="h-16 w-16 text-muted mx-auto mb-4" />
      <h3 className="text-lg font-medium text-secondary mb-2">
        {message || '暂不支持该文件类型预览'}
      </h3>
      <p className="text-muted text-sm">{fileName}</p>
    </div>
  );
}

function ImagePreview({ data }: { data: ArrayBuffer; fileName: string }) {
  const [url, setUrl] = useState('');
  useEffect(() => {
    const blob = new Blob([data]);
    const objectUrl = URL.createObjectURL(blob);
    setUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [data]);
  if (!url) return null;
  return (
    <div className="flex justify-center p-4">
      <img src={url} alt="preview" className="max-w-full max-h-[70vh] rounded" />
    </div>
  );
}

function TextPreview({ data }: { data: ArrayBuffer }) {
  const text = new TextDecoder().decode(data);
  return (
    <div className="text-viewer-content">
      <pre className="whitespace-pre-wrap text-sm text-secondary font-mono leading-relaxed p-4">
        {text}
      </pre>
    </div>
  );
}
