import { Error } from 'App/State/AppSectionState';
import { ValidationFailure } from 'typings/pending';
import { ApiError } from 'Utilities/Fetch/fetchJson';

function getMessageFromBody(body: unknown): string | undefined {
  if (Array.isArray(body)) {
    const messages = (body as ValidationFailure[])
      .map((failure) => failure?.errorMessage)
      .filter((message): message is string => Boolean(message));

    return messages.length ? messages.join(', ') : undefined;
  }

  if (body && typeof body === 'object' && 'message' in body) {
    const message = (body as { message?: string }).message;

    return message || undefined;
  }

  return undefined;
}

function getErrorMessage(
  error: Error | ApiError | undefined | null,
  fallbackErrorMessage = ''
) {
  if (!error) {
    return fallbackErrorMessage;
  }

  if (error instanceof ApiError) {
    return (
      getMessageFromBody(error.statusBody as unknown) ?? fallbackErrorMessage
    );
  }

  return (
    getMessageFromBody(error.responseJSON as unknown) ?? fallbackErrorMessage
  );
}

export default getErrorMessage;
