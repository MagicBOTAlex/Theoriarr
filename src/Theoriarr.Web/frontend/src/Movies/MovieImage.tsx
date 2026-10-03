import React, { useCallback, useEffect, useRef, useState } from 'react';
import LazyLoad from 'react-lazyload';
import getMediaCoverUrl from 'Utilities/MediaCover';
import { CoverType, MovieImage as MovieImageResource } from './Movie';

const defaultPlaceholder =
  'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7';

function findImage(images: MovieImageResource[], coverType: CoverType) {
  return images.find((image) => image.coverType === coverType);
}

function getUrl(
  url: string | undefined,
  remoteUrl: string | undefined,
  coverType: CoverType,
  size: number
) {
  const imageUrl = getMediaCoverUrl('movies', url || remoteUrl);

  return imageUrl
    ? imageUrl.replace(`${coverType}.jpg`, `${coverType}-${size}.jpg`)
    : null;
}

export interface MovieImageProps {
  className?: string;
  style?: object;
  images?: MovieImageResource[];
  coverType: CoverType;
  url?: string;
  remoteUrl?: string;
  placeholder?: string;
  size?: number;
  lazy?: boolean;
  overflow?: boolean;
  alt?: string;
  onError?: () => void;
  onLoad?: () => void;
}

const pixelRatio = Math.max(Math.round(window.devicePixelRatio), 1);

function MovieImage({
  className,
  style,
  images,
  coverType,
  url,
  remoteUrl,
  placeholder = defaultPlaceholder,
  size = 250,
  lazy = true,
  overflow = false,
  alt,
  onError,
  onLoad,
}: MovieImageProps) {
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [hasError, setHasError] = useState(false);
  const [isLoaded, setIsLoaded] = useState(true);
  const image = useRef<MovieImageResource | null>(null);

  const handleLoad = useCallback(() => {
    setHasError(false);
    setIsLoaded(true);
    onLoad?.();
  }, [onLoad]);

  const handleError = useCallback(() => {
    setHasError(true);
    setIsLoaded(false);
    onError?.();
  }, [onError]);

  useEffect(() => {
    const nextImage = images ? findImage(images, coverType) : undefined;
    const nextUrl = url ?? nextImage?.url;
    const nextRemoteUrl = remoteUrl ?? nextImage?.remoteUrl;

    if (nextUrl || nextRemoteUrl) {
      image.current = nextImage ?? null;
      setImageUrl(getUrl(nextUrl, nextRemoteUrl, coverType, pixelRatio * size));
      setHasError(false);
      setIsLoaded(true);
    } else if (image.current) {
      image.current = null;
      setImageUrl(placeholder);
      setHasError(false);
      onError?.();
    }
  }, [images, coverType, url, remoteUrl, placeholder, size, onError]);

  useEffect(() => {
    if (!image.current) {
      onError?.();
    }
    // This should only run once when the component mounts,
    // so we don't need to include the other dependencies.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (hasError || !imageUrl) {
    return (
      <img alt={alt} className={className} style={style} src={placeholder} />
    );
  }

  if (lazy) {
    return (
      <LazyLoad
        height={size}
        offset={100}
        overflow={overflow}
        placeholder={
          <img
            alt={alt}
            className={className}
            style={style}
            src={placeholder}
          />
        }
      >
        <img
          alt={alt}
          className={className}
          style={style}
          src={imageUrl}
          rel="noreferrer"
          onError={handleError}
          onLoad={handleLoad}
        />
      </LazyLoad>
    );
  }

  return (
    <img
      alt={alt}
      className={className}
      style={style}
      src={isLoaded ? imageUrl : placeholder}
      onError={handleError}
      onLoad={handleLoad}
    />
  );
}

export default MovieImage;
