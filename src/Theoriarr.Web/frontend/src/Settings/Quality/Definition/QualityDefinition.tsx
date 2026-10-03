import React, { useCallback } from 'react';
import TextInput from 'Components/Form/TextInput';
import Quality from 'Quality/Quality';
import { useManageQualityDefinitions } from './useQualityDefinitions';

interface QualityDefinitionProps {
  id: number;
  quality: Quality;
  title: string;
  updateDefinition: ReturnType<
    typeof useManageQualityDefinitions
  >['updateDefinition'];
}

function QualityDefinition({
  id,
  quality,
  title,
  updateDefinition,
}: QualityDefinitionProps) {
  const handleTitleChange = useCallback(
    ({ value }: { value: string }) => {
      updateDefinition(id, 'title', value);
    },
    [id, updateDefinition]
  );

  return (
    <div className="my-[5px] flex h-auto flex-wrap content-stretch border-t border-[color-mix(in_srgb,var(--color-base-200),transparent_50%)] pt-[5px] first:border-t-0 md:h-[45px] md:flex-nowrap">
      <div className="shrink basis-[250px] pr-5 font-bold leading-[inherit] md:font-normal md:leading-[40px]">
        {quality.name}
      </div>

      <div className="shrink basis-[250px] pr-5 leading-[40px]">
        <TextInput
          name={`${id}.${title}`}
          value={title}
          onChange={handleTitleChange}
        />
      </div>
    </div>
  );
}

export default QualityDefinition;
