import { useCallback, useEffect } from 'react';
import useTheme from 'Helpers/Hooks/useTheme';
import themes from 'Styles/Themes';

// daisyUI theme names registered in Styles/tailwind.css.
const DAISY_THEMES = {
  light: 'theoriarr-light',
  dark: 'theoriarr-dark',
};

function ApplyTheme() {
  const theme = useTheme();

  const updateCSSVariables = useCallback(() => {
    Object.entries(themes[theme]).forEach(([key, value]) => {
      document.documentElement.style.setProperty(`--${key}`, value);
    });

    // The application shell is styled with daisyUI, which reads its palette
    // from the selected `data-theme`; content still uses the CSS variables
    // above until it is migrated.
    document.documentElement.setAttribute('data-theme', DAISY_THEMES[theme]);
  }, [theme]);

  // On Component Mount and Component Update
  useEffect(() => {
    updateCSSVariables();
  }, [updateCSSVariables, theme]);

  return null;
}

export default ApplyTheme;
