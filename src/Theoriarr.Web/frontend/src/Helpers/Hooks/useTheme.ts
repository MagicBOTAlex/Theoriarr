import { useEffect, useState } from 'react';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import themes from 'Styles/Themes';

const useTheme = (): 'dark' | 'light' => {
  const { theme } = useUiSettingsValues();
  const selectedTheme = theme ?? window.Theoriarr.services.series.theme;
  const [resolvedTheme, setResolvedTheme] = useState(() => {
    if (selectedTheme === 'auto') {
      return window.matchMedia('(prefers-color-scheme: dark)').matches
        ? 'dark'
        : 'light';
    }

    return selectedTheme;
  });

  useEffect(() => {
    if (selectedTheme !== 'auto') {
      setResolvedTheme(selectedTheme);
      return;
    }

    const mediaQueryList = window.matchMedia('(prefers-color-scheme: dark)');

    const applySystemTheme = () => {
      setResolvedTheme(mediaQueryList.matches ? 'dark' : 'light');
    };

    applySystemTheme();

    mediaQueryList.addEventListener('change', applySystemTheme);

    return () => {
      mediaQueryList.removeEventListener('change', applySystemTheme);
    };
  }, [selectedTheme]);

  return resolvedTheme;
};

export default useTheme;

export const useThemeColor = (color: string) => {
  const theme = useTheme();
  const themeVariables = themes[theme];

  // @ts-expect-error - themeVariables is a string indexable type
  return themeVariables[color];
};
