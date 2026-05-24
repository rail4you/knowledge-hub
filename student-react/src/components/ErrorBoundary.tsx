import React, { Component, type ReactNode } from 'react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
  onError?: (error: Error, errorInfo: React.ErrorInfo) => void;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    // Log to console for debugging
    console.error('React ErrorBoundary caught an error:', error);
    console.error('Component stack:', errorInfo.componentStack);
    
    this.props.onError?.(error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback;
      }
      
      return (
        <div className="error-boundary">
          <div className="error-content">
            <h2>出错了</h2>
            <p>页面发生了错误，请刷新重试。</p>
            <button onClick={() => window.location.reload()}>刷新页面</button>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}

// Console error interceptor for development
export function setupConsoleErrorInterceptor() {
  if (process.env.NODE_ENV !== 'development') return;

  const originalError = console.error;
  
  console.error = (...args: unknown[]) => {
    // Filter out known non-critical warnings
    const message = args[0];
    if (typeof message === 'string') {
      // Ignore React dev mode warnings
      if (message.includes('Warning:') || message.includes('Download the React DevTools')) {
        return;
      }
      // Ignore hydration warnings in some cases
      if (message.includes('hydrating') || message.includes('Text content')) {
        return originalError(...args);
      }
    }
    
    originalError(...args);
  };
}