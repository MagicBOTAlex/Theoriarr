import classNames from 'classnames';
import React, { Children, ComponentPropsWithoutRef, ReactNode } from 'react';
import { Size } from 'Helpers/Props/sizes';

const GROUP_CLASS = 'mb-5 flex max-[1200px]:block';

const SIZE_CLASSES = {
  extraSmall: 'max-w-[550px]',
  small: 'max-w-[650px]',
  medium: 'max-w-[800px]',
  large: 'max-w-[1200px]',
};

interface FormGroupProps extends ComponentPropsWithoutRef<'div'> {
  className?: string;
  children: ReactNode;
  size?: Extract<Size, keyof typeof SIZE_CLASSES>;
  advancedSettings?: boolean;
  isAdvanced?: boolean;
}

function FormGroup(props: FormGroupProps) {
  const {
    className = GROUP_CLASS,
    children,
    size = 'small',
    advancedSettings = false,
    isAdvanced = false,
    ...otherProps
  } = props;

  if (!advancedSettings && isAdvanced) {
    return null;
  }

  const childProps = isAdvanced ? { isAdvanced } : {};

  return (
    <div className={classNames(className, SIZE_CLASSES[size])} {...otherProps}>
      {Children.map(children, (child) => {
        if (!React.isValidElement(child)) {
          return child;
        }

        return React.cloneElement(child, childProps);
      })}
    </div>
  );
}

export default FormGroup;
