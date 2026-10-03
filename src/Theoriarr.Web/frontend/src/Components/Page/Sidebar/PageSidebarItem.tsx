import classNames from 'classnames';
import React, { Children, useCallback } from 'react';
import Icon, { IconName } from 'Components/Icon';
import Link from 'Components/Link/Link';

const ITEM_CLASS = 'relative text-[var(--color-neutral-content)]';

const LINK_CLASS =
  'mx-2 my-0.5 flex items-center gap-2.5 rounded-lg px-3 py-2.5 text-[var(--color-neutral-content)] transition-colors duration-200 hover:bg-[color-mix(in_srgb,var(--color-neutral-content),transparent_90%)] hover:text-[var(--color-primary)] hover:no-underline focus-visible:bg-[color-mix(in_srgb,var(--color-neutral-content),transparent_90%)] focus-visible:text-[var(--color-primary)] focus-visible:no-underline focus-visible:outline-none';

const CHILD_LINK_CLASS = 'py-2 pr-3 pl-9 text-[13px]';

const ACTIVE_PARENT_LINK_CLASS =
  'bg-[color-mix(in_srgb,var(--color-neutral-content),transparent_92%)]';

const ACTIVE_LINK_CLASS =
  'bg-[color-mix(in_srgb,var(--color-primary),transparent_88%)] text-[var(--color-primary)]';

const ICON_CONTAINER_CLASS = 'inline-flex w-[18px] shrink-0 justify-center';

export interface PageSidebarItemProps {
  iconName?: IconName;
  title: string | (() => string);
  to: string;
  isActive?: boolean;
  isActiveParent?: boolean;
  isParentItem?: boolean;
  isChildItem?: boolean;
  statusComponent?: React.ElementType;
  children?: React.ReactNode;
  onPress?: () => void;
}

function PageSidebarItem({
  iconName,
  title,
  to,
  isActive,
  isActiveParent,
  isChildItem = false,
  isParentItem = false,
  statusComponent: StatusComponent,
  children,
  onPress,
}: PageSidebarItemProps) {
  const handlePress = useCallback(() => {
    if (isChildItem || !isParentItem) {
      onPress?.();
    }
  }, [isChildItem, isParentItem, onPress]);

  return (
    <div className={ITEM_CLASS}>
      <Link
        className={classNames(
          LINK_CLASS,
          isChildItem && CHILD_LINK_CLASS,
          isActiveParent && ACTIVE_PARENT_LINK_CLASS,
          isActive && ACTIVE_LINK_CLASS
        )}
        to={to}
        aria-current={isActive ? 'page' : undefined}
        onPress={handlePress}
      >
        {!!iconName && (
          <span className={ICON_CONTAINER_CLASS}>
            <Icon name={iconName} aria-hidden={true} />
          </span>
        )}

        <span className="min-w-0 truncate">
          {typeof title === 'function' ? title() : title}
        </span>

        {!!StatusComponent && (
          <span className="ml-auto inline-flex items-center">
            <StatusComponent />
          </span>
        )}
      </Link>

      {children
        ? Children.map(children, (child) => {
            if (!React.isValidElement(child)) {
              return child;
            }

            const childProps = { isChildItem: true };

            return React.cloneElement(child, childProps);
          })
        : null}
    </div>
  );
}

export default PageSidebarItem;
