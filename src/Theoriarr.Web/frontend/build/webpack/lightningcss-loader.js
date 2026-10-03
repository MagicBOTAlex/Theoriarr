/* eslint-disable @typescript-eslint/no-var-requires, filenames/match-exported */
// Runs the remaining non-Tailwind stylesheets through Lightning CSS (via
// `@tailwindcss/node`'s `optimize`) so they get the same nesting down-levelling
// and vendor prefixing as the Tailwind entry point, without adding PostCSS or a
// PostCSS loader back to the build.
const { optimize } = require('@tailwindcss/node');

module.exports = function lightningcssLoader(source) {
  const { code } = optimize(source, {
    file: this.resourcePath,
    minify: this.mode === 'production'
  });

  return code;
};
