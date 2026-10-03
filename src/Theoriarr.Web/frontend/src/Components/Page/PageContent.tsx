import React from 'react';
import DocumentTitle from 'react-document-title';
import ErrorBoundary from 'Components/Error/ErrorBoundary';
import PageContentError from './PageContentError';

interface PageContentProps {
  className?: string;
  title: string;
  children: React.ReactNode;
}

function PageContent({
  className = 'relative flex w-full grow flex-col overflow-x-hidden',
  title,
  children,
}: PageContentProps) {
  return (
    <ErrorBoundary errorComponent={PageContentError}>
      <DocumentTitle title={title ? `${title} - Theoriarr` : 'Theoriarr'}>
        <main className={className} aria-label={title}>
          {children}
        </main>
      </DocumentTitle>
    </ErrorBoundary>
  );
}

export default PageContent;
