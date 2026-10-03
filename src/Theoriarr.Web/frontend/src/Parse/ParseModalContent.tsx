import classNames from 'classnames';
import React, { useCallback, useState } from 'react';
import { INPUT_CLASS } from 'Components/Form/InputClassNames';
import TextInput from 'Components/Form/TextInput';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import LoadingIndicator, {
  LOADING_INDICATOR_CLASS,
} from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import useDebounce from 'Helpers/Hooks/useDebounce';
import { icons } from 'Helpers/Props';
import { InputChanged } from 'typings/inputs';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import ParseResult from './ParseResult';
import useParse from './useParse';

const PARSE_INPUT_CLASS = classNames(
  INPUT_CLASS,
  'h-[46px] rounded-none! text-[18px]'
);

const INPUT_CONTAINER_CLASS = 'flex mb-[10px]';

const INPUT_ICON_CONTAINER_CLASS =
  'w-[58px] h-[46px] border border-solid border-[var(--inputBorderColor)] border-r-0 rounded-[4px] rounded-tr-none rounded-br-none bg-[var(--inputIconContainerBackgroundColor)] text-center leading-[46px]';

const CLEAR_BUTTON_CLASS =
  'border border-solid border-[var(--inputBorderColor)] border-l-0! shadow-[inset_0_1px_1px_rgba(0,0,0,0.075)]';

const MESSAGE_CLASS = 'mt-[30px] text-center font-light text-[16px]';

const HELP_TEXT_CLASS = 'mb-[10px] text-[24px]';

const MODAL_FOOTER_EXTRA_CLASS = 'justify-between!';

interface ParseModalContentProps {
  onModalClose: () => void;
}

function ParseModalContent(props: ParseModalContentProps) {
  const { onModalClose } = props;

  const [title, setTitle] = useState('');
  const queryTitle = useDebounce(title, title ? 300 : 0);
  const { isFetching, isLoading, error, data } = useParse(queryTitle);

  const onInputChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setTitle(value);
    },
    [setTitle]
  );

  const onClearPress = useCallback(() => {
    setTitle('');
  }, [setTitle]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('TestParsing')}</ModalHeader>

      <ModalBody>
        <div className={INPUT_CONTAINER_CLASS}>
          <div className={INPUT_ICON_CONTAINER_CLASS}>
            <Icon name={icons.PARSE} size={20} />
          </div>

          <TextInput
            className={PARSE_INPUT_CLASS}
            name="title"
            value={title}
            placeholder="eg. Series.Title.S01E05.720p.HDTV-RlsGroup"
            autoFocus={true}
            onChange={onInputChange}
          />

          <Button className={CLEAR_BUTTON_CLASS} onPress={onClearPress}>
            <Icon name={icons.REMOVE} size={20} />
          </Button>
        </div>

        {isLoading ? <LoadingIndicator /> : null}

        {!isLoading && !!error ? (
          <div className={MESSAGE_CLASS}>
            <div className={HELP_TEXT_CLASS}>
              {translate('ParseModalErrorParsing')}
            </div>
            <div>{getErrorMessage(error)}</div>
          </div>
        ) : null}

        {!isLoading && title && !error && !data.parsedEpisodeInfo ? (
          <div className={MESSAGE_CLASS}>
            {translate('ParseModalUnableToParse')}
          </div>
        ) : null}

        {!isLoading && !error && data.parsedEpisodeInfo ? (
          <ParseResult item={data} />
        ) : null}

        {title ? null : (
          <div className={MESSAGE_CLASS}>
            <div className={HELP_TEXT_CLASS}>
              {translate('ParseModalHelpText')}
            </div>
            <div>{translate('ParseModalHelpTextDetails')}</div>
          </div>
        )}
      </ModalBody>

      <ModalFooter className={MODAL_FOOTER_EXTRA_CLASS}>
        <div>
          {isFetching && !isLoading ? (
            <LoadingIndicator
              className={classNames(LOADING_INDICATOR_CLASS, 'mt-0!')}
              size={20}
            />
          ) : null}
        </div>

        <Button onPress={onModalClose}>{translate('Close')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default ParseModalContent;
