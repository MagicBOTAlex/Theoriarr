import React, { useEffect, useState } from 'react';
import StackTrace from 'stacktrace-js';
import translate from 'Utilities/String/translate';

const CONTAINER_CLASS = 'text-center';

const MESSAGE_CLASS = 'my-[50px] text-center text-[36px] font-light';

const DETAILS_CLASS = 'm-5 text-left whitespace-pre-wrap';

export interface ErrorBoundaryErrorProps {
  className: string;
  messageClassName: string;
  detailsClassName: string;
  message: string;
  error: Error;
  info: {
    componentStack: string;
  };
}

function ErrorBoundaryError(props: ErrorBoundaryErrorProps) {
  const {
    className = CONTAINER_CLASS,
    messageClassName = MESSAGE_CLASS,
    detailsClassName = DETAILS_CLASS,
    message = translate('ErrorLoadingContent'),
    error,
    info,
  } = props;

  const [detailedError, setDetailedError] = useState<
    StackTrace.StackFrame[] | null
  >(null);

  useEffect(() => {
    if (error) {
      StackTrace.fromError(error).then((de) => {
        setDetailedError(de);
      });
    } else {
      setDetailedError(null);
    }
  }, [error, setDetailedError]);

  return (
    <div className={className}>
      <div className={messageClassName}>{message}</div>

      <div className="flex flex-[0_0_auto] justify-center">
        <img
          className="h-[350px] max-[992px]:h-[250px] max-[768px]:h-[150px]"
          src={`${window.Theoriarr.services.series.urlBase}/Content/Images/error.png`}
        />
      </div>

      <details className={detailsClassName}>
        {error ? <div>{error.message}</div> : null}

        {detailedError ? (
          detailedError.map((d, index) => {
            return (
              <div key={index}>
                {`  at ${d.functionName} (${d.fileName}:${d.lineNumber}:${d.columnNumber})`}
              </div>
            );
          })
        ) : (
          <div>{info.componentStack}</div>
        )}

        <div className="mt-5">
          Version: {window.Theoriarr.services.series.version}
        </div>
      </details>
    </div>
  );
}

export default ErrorBoundaryError;
