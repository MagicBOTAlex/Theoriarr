import React from 'react';
import PageContent from 'Components/Page/PageContent';
import translate from 'Utilities/String/translate';

interface NotFoundProps {
  message?: string;
}

function NotFound(props: NotFoundProps) {
  const { message = translate('DefaultNotFoundMessage') } = props;

  return (
    <PageContent title={translate('PageNotFound')}>
      <div className="text-center">
        <div className="my-[50px] text-center text-[36px] font-light">
          {message}
        </div>

        <img
          className="h-[350px]"
          src={`${window.Theoriarr.services.series.urlBase}/Content/Images/404.png`}
        />
      </div>
    </PageContent>
  );
}

export default NotFound;
