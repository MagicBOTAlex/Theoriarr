import React from 'react';
import ErrorBoundaryError, {
  ErrorBoundaryErrorProps,
} from 'Components/Error/ErrorBoundaryError';
import translate from 'Utilities/String/translate';
import PageContentBody from './PageContentBody';

function PageContentError(props: ErrorBoundaryErrorProps) {
  return (
    <div className="relative flex w-full grow flex-col overflow-x-hidden">
      <PageContentBody>
        <ErrorBoundaryError
          {...props}
          message={translate('ErrorLoadingPage')}
        />
      </PageContentBody>
    </div>
  );
}

export default PageContentError;
