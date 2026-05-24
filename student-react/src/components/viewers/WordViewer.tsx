import { useEffect, useState } from 'react';
import DOMPurify from 'dompurify';

interface WordViewerProps {
  data: ArrayBuffer;
  fileName: string;
}

export function WordViewer({ data }: WordViewerProps) {
  const [htmlContent, setHtmlContent] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    let cancelled = false;
    const parseWord = async () => {
      try {
        setIsLoading(true);
        setError('');
        const mammoth = await import('mammoth');
        const result = await mammoth.default.convertToHtml({ arrayBuffer: data });
        if (cancelled) return;
        const clean = DOMPurify.sanitize(result.value);
        setHtmlContent(clean);
        if (result.messages.length > 0) {
          console.warn('Mammoth warnings:', result.messages);
        }
      } catch (e: any) {
        if (cancelled) return;
        console.error('Word parse error:', e);
        setError(e.message || 'Word 文档解析失败');
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };
    parseWord();
    return () => { cancelled = true; };
  }, [data]);

  if (isLoading) {
    return (
      <div className="file-viewer-loading">
        <div className="loading-spinner" />
        <p className="mt-4 text-secondary text-sm">正在解析 Word 文档…</p>
      </div>
    );
  }

  if (error) {
    return <div className="file-viewer-error">{error}</div>;
  }

  return (
    <div className="word-viewer-content">
      <div
        className="word-viewer-html prose prose-sm max-w-none"
        dangerouslySetInnerHTML={{ __html: htmlContent }}
      />
    </div>
  );
}
