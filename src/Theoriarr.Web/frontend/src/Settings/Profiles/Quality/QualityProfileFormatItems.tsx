import React, { useMemo } from 'react';
import FormGroup from 'Components/Form/FormGroup';
import FormInputHelpText from 'Components/Form/FormInputHelpText';
import FormLabel from 'Components/Form/FormLabel';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import { sizes } from 'Helpers/Props';
import { QualityProfileFormatItem as QualityProfileFormatItemModel } from 'Settings/Profiles/Quality/useQualityProfiles';
import { Failure } from 'typings/pending';
import translate from 'Utilities/String/translate';
import QualityProfileFormatItem from './QualityProfileFormatItem';

interface QualityProfileFormatItemsProps {
  profileFormatItems: QualityProfileFormatItemModel[];
  errors?: Failure[];
  warnings?: Failure[];
  onQualityProfileFormatItemScoreChange: (
    formatId: number,
    score: number
  ) => void;
}

function QualityProfileFormatItems({
  profileFormatItems,
  errors = [],
  warnings = [],
  onQualityProfileFormatItemScoreChange,
}: QualityProfileFormatItemsProps) {
  const order = useMemo(() => {
    const items = profileFormatItems.reduce<Record<number, number>>(
      (acc, cur, index) => {
        acc[cur.format] = index;
        return acc;
      },
      {}
    );

    return [...profileFormatItems]
      .sort((a, b) => {
        if (b.score !== a.score) {
          return b.score - a.score;
        }

        return a.name.localeCompare(b.name, undefined, { numeric: true });
      })
      .map((x) => items[x.format]);
  }, [profileFormatItems]);

  if (profileFormatItems.length < 1) {
    return (
      <InlineMarkdown
        className="max-w-[550px] text-center text-[20px] font-light text-[var(--helpTextColor)]"
        data={translate('WantMoreControlAddACustomFormat')}
      />
    );
  }

  return (
    <FormGroup size={sizes.EXTRA_SMALL}>
      <FormLabel size={sizes.SMALL}>{translate('CustomFormats')}</FormLabel>

      <div>
        <FormInputHelpText text={translate('CustomFormatHelpText')} />

        {errors.map((error, index) => {
          return (
            <FormInputHelpText
              key={index}
              text={error.message}
              isError={true}
              isCheckInput={false}
            />
          );
        })}

        {warnings.map((warning, index) => {
          return (
            <FormInputHelpText
              key={index}
              text={warning.message}
              isWarning={true}
              isCheckInput={false}
            />
          );
        })}

        <div className="mt-[10px] select-none">
          <div className="flex font-bold leading-[35px]">
            <div className="flex grow">{translate('CustomFormat')}</div>
            <div className="flex w-[100px] grow-0 pl-4">
              {translate('Score')}
            </div>
          </div>

          {order.map((index) => {
            const { format, name, score } = profileFormatItems[index];
            return (
              <QualityProfileFormatItem
                key={format}
                formatId={format}
                name={name}
                score={score}
                onScoreChange={onQualityProfileFormatItemScoreChange}
              />
            );
          })}
        </div>
      </div>
    </FormGroup>
  );
}

export default QualityProfileFormatItems;
