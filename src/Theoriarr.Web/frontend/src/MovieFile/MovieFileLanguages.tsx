import React from 'react';
import Label from 'Components/Label';
import Popover from 'Components/Tooltip/Popover';
import { kinds, tooltipPositions } from 'Helpers/Props';
import Language from 'Language/Language';
import translate from 'Utilities/String/translate';

interface MovieFileLanguagesProps {
  className?: string;
  languages: Language[];
}

function MovieFileLanguages({ className, languages }: MovieFileLanguagesProps) {
  if (!languages || languages.length === 0) {
    return null;
  }

  if (languages.length === 1) {
    return (
      <Label className={className} kind={kinds.INVERSE}>
        {languages[0].name}
      </Label>
    );
  }

  return (
    <Popover
      className={className}
      anchor={
        <Label className={className} kind={kinds.INVERSE}>
          {translate('MultiLanguage')}
        </Label>
      }
      title={translate('Languages')}
      body={
        <ul>
          {languages.map((language) => (
            <li key={language.id}>{language.name}</li>
          ))}
        </ul>
      }
      position={tooltipPositions.LEFT}
    />
  );
}

export default MovieFileLanguages;
