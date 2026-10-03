import classNames from 'classnames';
import React, { useCallback, useEffect } from 'react';
import ReCAPTCHA from 'react-google-recaptcha';
import Icon from 'Components/Icon';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { icons } from 'Helpers/Props';
import { InputChanged } from 'typings/inputs';
import FormInputButton from './FormInputButton';
import {
  INPUT_BUTTON_CLASS,
  INPUT_CLASS,
  INPUT_ERROR_CLASS,
  INPUT_WARNING_CLASS,
} from './InputClassNames';
import TextInput from './TextInput';
import useCaptcha from './useCaptcha';

export interface CaptchaInputProps {
  className?: string;
  name: string;
  value?: string;
  provider: string;
  providerData: object;
  hasError?: boolean;
  hasWarning?: boolean;
  onChange: (change: InputChanged<string>) => unknown;
}

function CaptchaInput({
  className = INPUT_CLASS,
  name,
  value = '',
  provider,
  providerData,
  hasError,
  hasWarning,
  onChange,
}: CaptchaInputProps) {
  const {
    token,
    refreshing,
    siteKey,
    secretToken,
    refresh,
    getCaptchaCookie,
    reset,
  } = useCaptcha();
  const previousToken = usePrevious(token);

  const handleCaptchaChange = useCallback(
    (captchaResponse: string | null) => {
      // If the captcha has expired `captchaResponse` will be null.
      // In the event it's null don't try to get the captchaCookie.
      // TODO: Should we clear the cookie? or reset the captcha?

      if (!captchaResponse) {
        return;
      }

      getCaptchaCookie({
        provider,
        providerData,
        captchaResponse,
      });
    },
    [provider, providerData, getCaptchaCookie]
  );

  const handleRefreshPress = useCallback(() => {
    refresh({ provider, providerData });
  }, [provider, providerData, refresh]);

  useEffect(() => {
    if (token && token !== previousToken) {
      onChange({ name, value: token });
    }
  }, [name, token, previousToken, onChange]);

  useEffect(() => {
    reset();
  }, [reset]);

  return (
    <div>
      <div className="flex">
        <TextInput
          className={classNames(
            className,
            INPUT_BUTTON_CLASS,
            hasError && INPUT_ERROR_CLASS,
            hasWarning && INPUT_WARNING_CLASS
          )}
          name={name}
          value={value}
          onChange={onChange}
        />

        <FormInputButton onPress={handleRefreshPress}>
          <Icon name={icons.REFRESH} isSpinning={refreshing} />
        </FormInputButton>
      </div>

      {siteKey && secretToken ? (
        <div className="mt-[10px]">
          <ReCAPTCHA
            sitekey={siteKey}
            stoken={secretToken}
            onChange={handleCaptchaChange}
          />
        </div>
      ) : null}
    </div>
  );
}

export default CaptchaInput;
