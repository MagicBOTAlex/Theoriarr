/* eslint-disable @typescript-eslint/no-var-requires, filenames/match-exported */
// Compiles the Tailwind v4 entry stylesheet using Tailwind's own engine
// (`@tailwindcss/node` + the `@tailwindcss/oxide` scanner) so the build no
// longer needs PostCSS or a PostCSS loader. Mirrors what @tailwindcss/postcss
// does internally, minus PostCSS.
//
// `Scanner#scan()` does not read files from disk in this environment, so the
// candidate set is collected explicitly by feeding each scanned file's
// contents to `Scanner#scanFiles()`.
const fs = require('fs');
const path = require('path');
const { compile, optimize, Polyfills, Features } = require('@tailwindcss/node');
const { Scanner } = require('@tailwindcss/oxide');

function getRootSources(compiler) {
  if (compiler.root === 'none') {
    return [];
  }

  if (compiler.root === null) {
    return [{ base: process.cwd(), pattern: '**/*', negated: false }];
  }

  return [{ ...compiler.root, negated: false }];
}

function hasUtilities(features) {
  // eslint-disable-next-line no-bitwise
  return (Number(features) & Number(Features.Utilities)) !== 0;
}

module.exports = function tailwindLoader(source) {
  const callback = this.async();
  const isProduction = this.mode === 'production';
  const from = this.resourcePath;
  const base = path.dirname(from);

  compile(source, {
    from,
    base,
    polyfills: Polyfills.All,
    shouldRewriteUrls: true,
    onDependency: (dependency) => this.addDependency(dependency)
  })
    .then((compiler) => {
      const scanner = new Scanner({
        sources: getRootSources(compiler).concat(compiler.sources)
      });

      const scannedFiles = scanner.files;

      for (const file of scannedFiles) {
        this.addDependency(file);
      }

      let candidates = [];

      if (hasUtilities(compiler.features)) {
        const changedContent = scannedFiles.map((file) => ({
          content: fs.readFileSync(file, 'utf8'),
          extension: path.extname(file).slice(1)
        }));

        candidates = scanner.scanFiles(changedContent);
      }

      let css = compiler.build(candidates);

      if (isProduction) {
        css = optimize(css, { minify: true }).code;
      }

      callback(null, css);
    })
    .catch((error) => callback(error));
};
