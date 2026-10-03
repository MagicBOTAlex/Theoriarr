import classNames from 'classnames';
import React, { HTMLProps, useCallback, useState } from 'react';
import ReactSlider from 'react-slider';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import { INPUT_CLASS } from 'Components/Form/InputClassNames';
import NumberInput from 'Components/Form/NumberInput';
import Label from 'Components/Label';
import Popover from 'Components/Tooltip/Popover';
import { kinds, tooltipPositions } from 'Helpers/Props';
import QualityDefinitionLimits from 'Settings/Quality/Definition/QualityDefinitionLimits';
import { InputChanged } from 'typings/inputs';
import { Failure } from 'typings/pending';
import formatBytes from 'Utilities/Number/formatBytes';
import roundNumber from 'Utilities/Number/roundNumber';
import translate from 'Utilities/String/translate';
import { emptyItemFailures, ItemFailures } from './qualityProfileItemFailures';

const SIZE_LIMIT_CLASS = 'flex-[0_1_500px]';
const SLIDER_CLASS = 'w-full h-[20px]';
const TRACK_CLASS =
  'top-[9px] m-[0_5px] h-[3px] bg-[var(--sliderAccentColor)] shadow-[0_0_0_#000] [&:nth-child(3n+1)]:bg-[#ddd]';
const THUMB_CLASS =
  'top-[1px] z-0! w-[18px] h-[18px] border-[3px] border-solid border-[var(--sliderAccentColor)] rounded-full bg-[var(--white)] text-center cursor-pointer';
const SIZES_CLASS = 'flex justify-between';
const MEGABYTES_PER_MINUTE_CONTAINER_CLASS =
  'flex justify-between flex-[0_0_400px] mt-[10px] pt-[10px] border-t border-solid border-t-[var(--borderColor)]';
const MEGABYTES_PER_MINUTE_CLASS = 'flex flex-col';
const FAILURES_CLASS = 'flex-[0_0_400px] mt-[5px]';

const SIZE_INPUT_CLASS = classNames(
  INPUT_CLASS,
  'inline-block grow mb-[2px] p-[6px]!'
);

const MIN = 0;
const MAX = 400;
const STEP_SIZE = 0.1;
const MIN_DISTANCE = 3;
const SLIDER_MAX = roundNumber(Math.pow(MAX, 1 / 1.1));
const SLIDER_PREFERRED_DEFAULT = SLIDER_MAX - MIN_DISTANCE;

interface SizeProps {
  minSize: number | null;
  preferredSize: number | null;
  maxSize: number | null;
}

export interface SizeChanged extends SizeProps {
  qualityId: number;
}

export interface QualityProfileItemSizeProps {
  id: number;
  minSize: number | null;
  preferredSize: number | null;
  maxSize: number | null;
  failures?: ItemFailures;
  onSizeChange: (props: SizeChanged) => void;
}

function trackRenderer(props: HTMLProps<HTMLDivElement>) {
  return <div {...props} className={TRACK_CLASS} />;
}

function thumbRenderer(props: HTMLProps<HTMLDivElement>) {
  return <div {...props} className={THUMB_CLASS} />;
}

function getSliderValue(value: number | null, defaultValue: number): number {
  const sliderValue =
    value != null && value > 0 ? Math.pow(value, 1 / 1.1) : defaultValue;

  return roundNumber(sliderValue);
}

function renderFailures(failures: Failure[], isError: boolean) {
  return failures.map((failure, index) => {
    return (
      <FormInputHelpText
        key={`${isError ? 'error' : 'warning'}-${index}`}
        text={failure.message}
        isError={isError}
        isWarning={!isError}
        isCheckInput={false}
      />
    );
  });
}

export default function QualityProfileItemSize({
  id,
  minSize,
  maxSize,
  preferredSize,
  failures = emptyItemFailures(),
  onSizeChange,
}: QualityProfileItemSizeProps) {
  const [sizes, setSizes] = useState<SizeProps>({
    minSize: getSliderValue(minSize, MIN),
    preferredSize: getSliderValue(preferredSize, SLIDER_PREFERRED_DEFAULT),
    maxSize: getSliderValue(maxSize, SLIDER_MAX),
  });

  const handleSliderChange = useCallback(
    ([sliderMinSize, sliderPreferredSize, sliderMaxSize]: [
      number,
      number,
      number
    ]) => {
      setSizes({
        minSize: sliderMinSize,
        preferredSize: sliderPreferredSize,
        maxSize: sliderMaxSize,
      });

      onSizeChange?.({
        qualityId: id,
        minSize: roundNumber(Math.pow(sliderMinSize, 1.1)),
        preferredSize:
          sliderPreferredSize === SLIDER_PREFERRED_DEFAULT
            ? null
            : roundNumber(Math.pow(sliderPreferredSize, 1.1)),
        maxSize:
          sliderMaxSize === SLIDER_MAX
            ? null
            : roundNumber(Math.pow(sliderMaxSize, 1.1)),
      });
    },
    [id, setSizes, onSizeChange]
  );

  const handleMinSizeChange = useCallback(
    ({ value }: InputChanged<number>) => {
      setSizes({
        minSize: value,
        preferredSize: sizes.preferredSize,
        maxSize: sizes.maxSize,
      });

      onSizeChange?.({
        qualityId: id,
        minSize: value,
        preferredSize: sizes.preferredSize,
        maxSize: sizes.maxSize,
      });
    },
    [id, sizes, setSizes, onSizeChange]
  );

  const handlePreferredSizeChange = useCallback(
    ({ value }: InputChanged<number>) => {
      setSizes({
        minSize: sizes.minSize,
        preferredSize: value,
        maxSize: sizes.maxSize,
      });

      onSizeChange?.({
        qualityId: id,
        minSize: sizes.minSize,
        preferredSize: value,
        maxSize: sizes.maxSize,
      });
    },
    [id, sizes, setSizes, onSizeChange]
  );

  const handleMaxSizeChange = useCallback(
    ({ value }: InputChanged<number>) => {
      setSizes({
        minSize: sizes.minSize,
        preferredSize: sizes.preferredSize,
        maxSize: value,
      });

      onSizeChange?.({
        qualityId: id,
        minSize: sizes.minSize,
        preferredSize: sizes.preferredSize,
        maxSize: value,
      });
    },
    [id, sizes, setSizes, onSizeChange]
  );

  const handleAfterSliderChange = useCallback(() => {
    setSizes({
      minSize: getSliderValue(minSize, MIN),
      maxSize: getSliderValue(maxSize, SLIDER_MAX),
      preferredSize: getSliderValue(preferredSize, SLIDER_PREFERRED_DEFAULT),
    });
  }, [minSize, maxSize, preferredSize, setSizes]);

  const minBytes = (sizes.minSize ?? 0) * 1024 * 1024;
  const minSixty = `${formatBytes(minBytes * 60)}/${translate(
    'HourShorthand'
  )}`;

  const preferredBytes = (sizes.preferredSize ?? 0) * 1024 * 1024;
  const preferredSixty = preferredBytes
    ? `${formatBytes(preferredBytes * 60)}/${translate('HourShorthand')}`
    : translate('Unlimited');

  const maxBytes = maxSize == null ? null : maxSize * 1024 * 1024;
  const maxSixty = maxBytes
    ? `${formatBytes(maxBytes * 60)}/${translate('HourShorthand')}`
    : translate('Unlimited');

  const allFailures = [
    ...failures.errors.minSize,
    ...failures.errors.preferredSize,
    ...failures.errors.maxSize,
    ...failures.warnings.minSize,
    ...failures.warnings.preferredSize,
    ...failures.warnings.maxSize,
  ];

  return (
    <div className={SIZE_LIMIT_CLASS}>
      {/* eslint-disable-next-line @typescript-eslint/ban-ts-comment */}
      {/* @ts-ignore React version mismatch */}
      <ReactSlider
        className={SLIDER_CLASS}
        min={MIN}
        max={SLIDER_MAX}
        step={STEP_SIZE}
        minDistance={3}
        value={[sizes.minSize, sizes.preferredSize, sizes.maxSize]}
        withTracks={true}
        // eslint-disable-next-line @typescript-eslint/ban-ts-comment
        // @ts-ignore allowCross is still available in the version currently used
        allowCross={false}
        snapDragDisabled={true}
        pearling={true}
        renderThumb={thumbRenderer}
        renderTrack={trackRenderer}
        onChange={handleSliderChange}
        onAfterChange={handleAfterSliderChange}
      />

      <div className={SIZES_CLASS}>
        <div>
          <Popover
            anchor={<Label kind={kinds.INFO}>{minSixty}</Label>}
            title={translate('MinimumLimits')}
            body={
              <QualityDefinitionLimits
                bytes={minBytes}
                message={translate('NoMinimumForAnyRuntime')}
              />
            }
            position={tooltipPositions.BOTTOM}
          />
        </div>

        <div>
          <Popover
            anchor={<Label kind={kinds.SUCCESS}>{preferredSixty}</Label>}
            title={translate('PreferredSize')}
            body={
              <QualityDefinitionLimits
                bytes={preferredBytes}
                message={translate('NoLimitForAnyRuntime')}
              />
            }
            position={tooltipPositions.BOTTOM}
          />
        </div>

        <div>
          <Popover
            anchor={<Label kind={kinds.WARNING}>{maxSixty}</Label>}
            title={translate('MaximumLimits')}
            body={
              <QualityDefinitionLimits
                bytes={maxBytes ?? 0}
                message={translate('NoLimitForAnyRuntime')}
              />
            }
            position={tooltipPositions.BOTTOM}
          />
        </div>
      </div>

      <div className={MEGABYTES_PER_MINUTE_CONTAINER_CLASS}>
        <div className={MEGABYTES_PER_MINUTE_CLASS}>
          <NumberInput
            className={SIZE_INPUT_CLASS}
            name={`${id}.min`}
            value={minSize ?? MIN}
            min={MIN}
            max={preferredSize ? preferredSize - 5 : MAX - 5}
            step={0.1}
            isFloat={true}
            // @ts-expect-error - Typings are too loose
            onChange={handleMinSizeChange}
          />
          <Label kind={kinds.INFO}>
            {translate('Minimum')} MiB/
            {translate('MinuteShorthand')}
          </Label>
        </div>

        <div className={MEGABYTES_PER_MINUTE_CLASS}>
          <NumberInput
            className={SIZE_INPUT_CLASS}
            name={`${id}.preferred`}
            value={preferredSize ?? MAX - 5}
            min={MIN}
            max={maxSize ? maxSize - 5 : MAX - 5}
            step={0.1}
            isFloat={true}
            // @ts-expect-error - Typings are too loose
            onChange={handlePreferredSizeChange}
          />

          <Label kind={kinds.SUCCESS}>
            {translate('Preferred')} MiB/
            {translate('MinuteShorthand')}
          </Label>
        </div>

        <div className={MEGABYTES_PER_MINUTE_CLASS}>
          <NumberInput
            className={SIZE_INPUT_CLASS}
            name={`${id}.max`}
            value={maxSize ?? MAX}
            min={(preferredSize ?? 0) + STEP_SIZE}
            max={MAX}
            step={0.1}
            isFloat={true}
            // @ts-expect-error - Typings are too loose
            onChange={handleMaxSizeChange}
          />

          <Label kind={kinds.WARNING}>
            {translate('Maximum')} MiB/
            {translate('MinuteShorthand')}
          </Label>
        </div>
      </div>

      {allFailures.length > 0 ? (
        <div className={FAILURES_CLASS}>
          {renderFailures(failures.errors.minSize, true)}
          {renderFailures(failures.errors.preferredSize, true)}
          {renderFailures(failures.errors.maxSize, true)}
          {renderFailures(failures.warnings.minSize, false)}
          {renderFailures(failures.warnings.preferredSize, false)}
          {renderFailures(failures.warnings.maxSize, false)}
        </div>
      ) : null}
    </div>
  );
}
