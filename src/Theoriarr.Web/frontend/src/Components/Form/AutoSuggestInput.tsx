import { autoUpdate, flip, size, useFloating } from '@floating-ui/react-dom';
import classNames from 'classnames';
import React, {
  FocusEvent,
  FormEvent,
  KeyboardEvent,
  KeyboardEventHandler,
  MutableRefObject,
  ReactNode,
  Ref,
  SyntheticEvent,
  useCallback,
  useEffect,
  useRef,
} from 'react';
import Autosuggest, {
  AutosuggestPropsBase,
  BlurEvent,
  ChangeEvent,
  RenderInputComponentProps,
  RenderSuggestionsContainerParams,
} from 'react-autosuggest';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { InputChanged } from 'typings/inputs';
import {
  INPUT_BORDER_VISIBLE_CLASS,
  INPUT_CLASS,
  INPUT_ERROR_CLASS,
  INPUT_WARNING_CLASS,
} from './InputClassNames';

const SUGGESTIONS_CONTAINER_CLASS =
  '[scrollbar-color:var(--scrollbarBackgroundColor)_transparent] [scrollbar-width:thin] [&::-webkit-scrollbar]:w-[10px] [&::-webkit-scrollbar]:h-[10px] [&::-webkit-scrollbar-track]:bg-transparent [&::-webkit-scrollbar-thumb]:min-h-[100px] [&::-webkit-scrollbar-thumb]:border [&::-webkit-scrollbar-thumb]:border-solid [&::-webkit-scrollbar-thumb]:border-transparent [&::-webkit-scrollbar-thumb]:rounded-[5px] [&::-webkit-scrollbar-thumb]:bg-[var(--scrollbarBackgroundColor)] [&::-webkit-scrollbar-thumb]:bg-clip-padding [&::-webkit-scrollbar-thumb:hover]:bg-[var(--scrollbarHoverBackgroundColor)]';
const SUGGESTIONS_CONTAINER_OPEN_CLASS = classNames(
  'overflow-y-auto max-h-[200px] w-full border border-solid rounded-[4px] bg-[var(--inputBackgroundColor)] shadow-[inset_0_1px_1px_var(--inputBoxShadowColor)]',
  INPUT_BORDER_VISIBLE_CLASS
);
const SUGGESTIONS_CONTAINER_OPEN_WRAPPER_CLASS = 'z-[2000]';
const SUGGESTIONS_LIST_CLASS = 'my-[5px] pl-0 max-h-[200px] list-none';
const SUGGESTION_CLASS = 'px-[16px]';
const SUGGESTION_HIGHLIGHTED_CLASS = 'bg-[var(--menuItemHoverBackgroundColor)]';

interface AutoSuggestInputProps<T>
  extends Omit<AutosuggestPropsBase<T>, 'renderInputComponent' | 'inputProps'> {
  forwardedRef?: MutableRefObject<Autosuggest<T> | null>;
  className?: string;
  inputContainerClassName?: string;
  name: string;
  value?: string;
  placeholder?: string;
  suggestions: T[];
  hasError?: boolean;
  hasWarning?: boolean;
  enforceMaxHeight?: boolean;
  maxHeight?: number;
  renderInputComponent?: (
    inputProps: RenderInputComponentProps,
    ref: Ref<HTMLDivElement>
  ) => ReactNode;
  onInputChange: (
    event: FormEvent<HTMLElement>,
    params: ChangeEvent
  ) => unknown;
  onInputKeyDown?: KeyboardEventHandler<HTMLElement>;
  onInputFocus?: (event: SyntheticEvent) => unknown;
  onInputBlur: (
    event: FocusEvent<HTMLElement>,
    params?: BlurEvent<T>
  ) => unknown;
  onChange?: (change: InputChanged<T>) => unknown;
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function AutoSuggestInput<T = any>(props: AutoSuggestInputProps<T>) {
  const {
    // TODO: forwaredRef should be replaces with React.forwardRef
    forwardedRef,
    className = INPUT_CLASS,
    inputContainerClassName = 'grow',
    name,
    value = '',
    placeholder,
    suggestions,
    enforceMaxHeight = true,
    hasError,
    hasWarning,
    maxHeight = 200,
    getSuggestionValue,
    renderSuggestion,
    renderInputComponent,
    onInputChange,
    onInputKeyDown,
    onInputFocus,
    onInputBlur,
    onSuggestionsFetchRequested,
    onSuggestionsClearRequested,
    onSuggestionSelected,
    onChange,
    ...otherProps
  } = props;

  const updater = useRef<(() => void) | null>(null);
  const previousSuggestions = usePrevious(suggestions);

  const { refs, floatingStyles } = useFloating({
    middleware: [
      flip({
        crossAxis: false,
        mainAxis: true,
      }),
      size({
        apply({ availableHeight, elements, rects }) {
          Object.assign(elements.floating.style, {
            minWidth: `${rects.reference.width}px`,
            maxHeight: `${Math.max(0, availableHeight)}px`,
          });
        },
      }),
    ],
    placement: 'bottom-start',
    whileElementsMounted: autoUpdate,
  });

  const createRenderInputComponent = useCallback(
    (inputProps: RenderInputComponentProps) => {
      if (renderInputComponent) {
        return renderInputComponent(inputProps, refs.setReference);
      }

      return (
        <div ref={refs.setReference}>
          <input {...inputProps} />
        </div>
      );
    },
    [refs.setReference, renderInputComponent]
  );

  const renderSuggestionsContainer = useCallback(
    ({ containerProps, children }: RenderSuggestionsContainerParams) => {
      return (
        <div
          ref={refs.setFloating}
          style={floatingStyles}
          className={
            children ? SUGGESTIONS_CONTAINER_OPEN_WRAPPER_CLASS : undefined
          }
        >
          <div
            {...containerProps}
            className={classNames(
              containerProps.className,
              children && SUGGESTIONS_CONTAINER_OPEN_CLASS
            )}
            style={{
              maxHeight: enforceMaxHeight ? maxHeight : undefined,
            }}
          >
            {children}
          </div>
        </div>
      );
    },
    [enforceMaxHeight, floatingStyles, maxHeight, refs.setFloating]
  );

  const handleInputKeyDown = useCallback(
    (event: KeyboardEvent<HTMLElement>) => {
      if (
        event.key === 'Tab' &&
        suggestions.length &&
        suggestions[0] !== value
      ) {
        event.preventDefault();

        if (value) {
          onSuggestionSelected?.(event, {
            suggestion: suggestions[0],
            suggestionValue: value,
            suggestionIndex: 0,
            sectionIndex: null,
            method: 'enter',
          });
        }
      }
    },
    [value, suggestions, onSuggestionSelected]
  );

  const inputProps = {
    className: classNames(
      className,
      hasError && INPUT_ERROR_CLASS,
      hasWarning && INPUT_WARNING_CLASS
    ),
    name,
    value,
    placeholder,
    autoComplete: 'off',
    spellCheck: false,
    onChange: onInputChange,
    onKeyDown: onInputKeyDown || handleInputKeyDown,
    onFocus: onInputFocus,
    onBlur: onInputBlur,
  };

  const theme = {
    container: inputContainerClassName,
    containerOpen: SUGGESTIONS_CONTAINER_OPEN_WRAPPER_CLASS,
    suggestionsContainer: SUGGESTIONS_CONTAINER_CLASS,
    suggestionsList: SUGGESTIONS_LIST_CLASS,
    suggestion: SUGGESTION_CLASS,
    suggestionHighlighted: SUGGESTION_HIGHLIGHTED_CLASS,
  };

  useEffect(() => {
    if (updater.current && suggestions !== previousSuggestions) {
      updater.current();
    }
  }, [suggestions, previousSuggestions]);

  return (
    <Autosuggest
      {...otherProps}
      ref={forwardedRef}
      id={name}
      inputProps={inputProps}
      theme={theme}
      suggestions={suggestions}
      getSuggestionValue={getSuggestionValue}
      renderInputComponent={createRenderInputComponent}
      renderSuggestionsContainer={renderSuggestionsContainer}
      renderSuggestion={renderSuggestion}
      onSuggestionSelected={onSuggestionSelected}
      onSuggestionsFetchRequested={onSuggestionsFetchRequested}
      onSuggestionsClearRequested={onSuggestionsClearRequested}
    />
  );
}

export default AutoSuggestInput;
