import $ from 'jquery';
import { getService, getServiceClientHeader } from 'Services';
import createAjaxRequest from './createAjaxRequest';

function flattenProviderData(providerData) {
  return Object.keys(providerData).reduce((acc, key) => {
    const property = providerData[key];

    if (key === 'fields') {
      acc[key] = property;
    } else {
      acc[key] = property.value;
    }

    return acc;
  }, {});
}

function requestAction(payload) {
  const {
    provider,
    action,
    providerData,
    queryParams,
    service
  } = payload;

  const { apiKey } = getService(service);

  const ajaxOptions = {
    url: `/${provider}/action/${action}`,
    contentType: 'application/json',
    method: 'POST',
    headers: {
      'X-Api-Key': apiKey,
      ...getServiceClientHeader(service)
    },
    data: JSON.stringify(flattenProviderData(providerData))
  };

  if (queryParams) {
    ajaxOptions.url += `?${$.param(queryParams, true)}`;
  }

  return createAjaxRequest(ajaxOptions).request;
}

export default requestAction;
