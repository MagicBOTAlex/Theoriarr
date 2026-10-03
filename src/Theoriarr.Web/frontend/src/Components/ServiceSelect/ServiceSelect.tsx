import React, { useCallback } from 'react';
import Button from 'Components/Link/Button';
import { kinds, sizes } from 'Helpers/Props';
import { ServiceId } from 'Services';

const OPTIONS: { value: ServiceId; label: string }[] = [
  { value: 'series', label: 'Series' },
  { value: 'movies', label: 'Movies' },
];

interface ServiceSelectButtonProps {
  value: ServiceId;
  isActive: boolean;
  onSelect: (value: ServiceId) => void;
  children: React.ReactNode;
}

function ServiceSelectButton({
  value,
  isActive,
  onSelect,
  children,
}: ServiceSelectButtonProps) {
  const handlePress = useCallback(() => {
    onSelect(value);
  }, [onSelect, value]);

  return (
    <Button
      className="mr-1.5"
      kind={isActive ? kinds.PRIMARY : kinds.DEFAULT}
      size={sizes.SMALL}
      onPress={handlePress}
    >
      {children}
    </Button>
  );
}

interface ServiceSelectProps {
  value: ServiceId;
  label?: string;
  onChange: (value: ServiceId) => void;
}

// Shared Series/Movies switch used by Settings and by the unified Library,
// Add New and Library Import pages.
function ServiceSelect({ value, label, onChange }: ServiceSelectProps) {
  return (
    <div className="mr-3 flex items-center">
      {label ? (
        <span className="mr-2 text-[12px] text-[var(--helpTextColor)] uppercase">
          {label}
        </span>
      ) : null}

      {OPTIONS.map((option) => (
        <ServiceSelectButton
          key={option.value}
          value={option.value}
          isActive={value === option.value}
          onSelect={onChange}
        >
          {option.label}
        </ServiceSelectButton>
      ))}
    </div>
  );
}

export default ServiceSelect;
