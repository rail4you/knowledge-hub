interface Props {
  src: string;
  title?: string;
}

export function VideoPlayer({ src, title }: Props) {
  return (
    <div className="rounded-lg overflow-hidden bg-black">
      {/* eslint-disable-next-line jsx-a11y/media-has-caption */}
      <video
        src={src}
        controls
        className="w-full"
        style={{ maxHeight: '480px' }}
        preload="metadata"
      >
        {title && <track kind="metadata" label={title} />}
        您的浏览器不支持视频播放。
      </video>
      {title && (
        <div className="px-4 py-2 bg-white text-sm text-[#666]">{title}</div>
      )}
    </div>
  );
}
