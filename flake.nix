{
  description = "Theoriarr — one unified backend and one UI for movies, TV and anime";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  };

  outputs =
    { self, nixpkgs }:
    let
      systems = [
        "x86_64-linux"
        "aarch64-linux"
        "aarch64-darwin"
      ];

      forAllSystems =
        f: nixpkgs.lib.genAttrs systems (system: f system (import nixpkgs { inherit system; }));

      # Everything is derived from one package set so the shell, packages and
      # apps stay consistent.
      mkTools =
        pkgs:
        let
          lib = pkgs.lib;

          # The unified backend targets net10.0 (src/Theoriarr.Series pins SDK
          # 10.0.401 via global.json). The retired Movies tree is no longer
          # built, so only the .NET 10 SDK is needed.
          dotnet = pkgs.writeShellScriptBin "dotnet" ''
            root="${pkgs.dotnet-sdk_10}"
            export DOTNET_ROOT="$root/share/dotnet"
            export DOTNET_HOST_PATH="$root/share/dotnet/dotnet"
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
            exec "$root/bin/dotnet" "$@"
          '';

          toolchain = [
            dotnet
            pkgs.nodejs_24
            pkgs.yarn
            pkgs.python3
            pkgs.curl
            pkgs.git
            pkgs.jq
            pkgs.pkg-config
            pkgs.coreutils
            pkgs.util-linux
            pkgs.cacert
            # Media compression: ffmpeg (NVENC/VA-API/software) resolved from PATH;
            # libva-utils supplies `vainfo` for VA-API diagnosis. On NixOS the host
            # still provides the GPU driver/render nodes (`/dev/dri`).
            pkgs.ffmpeg
            pkgs.libva-utils
          ];

          # Wrapper that runs a repo script against the user's checkout rather
          # than the read-only store copy. `args` are fixed arguments appended
          # to the script invocation.
          mkApp =
            name: script: args:
            pkgs.writeShellApplication {
              inherit name;
              runtimeInputs = toolchain;
              text = ''
                repo="$(git rev-parse --show-toplevel 2>/dev/null || echo "$PWD")"
                exec "$repo/${script}" ${args}
              '';
            };

          # `nix run` attaches to the service logs in the foreground; Ctrl+C
          # stops everything.
          run = mkApp "theoriarr" "theoriarr.sh" "--attach";
          build = mkApp "theoriarr-build" "build.sh" "";
          test = mkApp "theoriarr-test" "test.sh" "";
          stop = mkApp "theoriarr-stop" "theoriarr-stop.sh" "";

          # Compiles the unified backend and the unified SPA and installs the
          # output tree. Needs network for NuGet + yarn, so it builds with the
          # sandbox disabled (see nix.conf).
          theoriarr = pkgs.stdenv.mkDerivation (finalAttrs: {
            pname = "theoriarr";
            version = "0.1.0";
            src = ./.;

            nativeBuildInputs = toolchain;

            env = {
              DOTNET_CLI_TELEMETRY_OPTOUT = "1";
              DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1";
              NODE_OPTIONS = "--max-old-space-size=8192";
              SSL_CERT_FILE = "${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt";
              NODE_EXTRA_CA_CERTS = "${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt";
              GIT_SSL_CAINFO = "${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt";
              CURL_CA_BUNDLE = "${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt";
            };

            buildPhase = ''
              runHook preBuild
              export HOME="$TMPDIR"
              export YARN_CACHE_FOLDER="$TMPDIR/yarn-cache"
              export NUGET_PACKAGES="$TMPDIR/nuget"
              bash build.sh
              runHook postBuild
            '';

            installPhase = ''
              runHook preInstall
              mkdir -p "$out"

              # One backend: the unified engine.
              mkdir -p "$out/src/Theoriarr.Series"
              cp -a src/Theoriarr.Series/_output "$out/src/Theoriarr.Series/_output"

              # One UI: the unified SPA.
              mkdir -p "$out/src/Theoriarr.Web/_output"
              cp -a src/Theoriarr.Web/_output/UI "$out/src/Theoriarr.Web/_output/UI"

              # Stage the unified UI into the backend's served UI folder
              # (<StartUpFolder>/UI) so the backend serves it directly at root.
              rm -rf "$out/src/Theoriarr.Series/_output/net10.0/UI"
              mkdir -p "$out/src/Theoriarr.Series/_output/net10.0/UI"
              cp -a src/Theoriarr.Web/_output/UI/. "$out/src/Theoriarr.Series/_output/net10.0/UI/"

              cp -a \
                theoriarr.sh theoriarr-stop.sh build.sh test.sh \
                Logo README.md LICENSE NOTICE "$out/"
              runHook postInstall
            '';

            meta = with lib; {
              description = "One unified backend and one UI for movies, TV and anime";
              license = licenses.gpl3Plus;
              platforms = platforms.unix;
            };
          });
        in
        {
          inherit
            toolchain
            run
            build
            test
            stop
            theoriarr
            dotnet
            ;
        };
    in
    {
      devShells = forAllSystems (
        system: pkgs:
        let
          tools = mkTools pkgs;
        in
        {
          default = pkgs.mkShell {
            packages = tools.toolchain;
            shellHook = ''
              export SSL_CERT_FILE="${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt"
              export NODE_EXTRA_CA_CERTS="${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt"
              export GIT_SSL_CAINFO="${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt"
              export CURL_CA_BUNDLE="${pkgs.cacert}/etc/ssl/certs/ca-bundle.crt"
              echo "Theoriarr dev shell"
              echo "  dotnet: $(dotnet --version 2>/dev/null || echo '(missing)')"
              echo "  node:   $(node --version 2>/dev/null || echo '(missing)')"
              echo "  yarn:   $(yarn --version 2>/dev/null || echo '(missing)')"
              echo "  python: $(python3 --version 2>/dev/null || echo '(missing)')"
              echo
              echo "  nix run .#build   build the unified backend + UI"
              echo "  nix run           start (foreground, Ctrl+C stops)"
            '';
          };
        }
      );

      packages = forAllSystems (
        system: pkgs:
        let
          tools = mkTools pkgs;
        in
        {
          default = tools.theoriarr;
          theoriarr = tools.theoriarr;
          run = tools.run;
        }
      );

      apps = forAllSystems (
        system: pkgs:
        let
          tools = mkTools pkgs;
        in
        {
          default = {
            type = "app";
            program = "${tools.run}/bin/theoriarr";
            meta.description = "Start Theoriarr (foreground)";
          };
          build = {
            type = "app";
            program = "${tools.build}/bin/theoriarr-build";
            meta.description = "Build the Theoriarr backend + UI";
          };
          test = {
            type = "app";
            program = "${tools.test}/bin/theoriarr-test";
            meta.description = "Run the Theoriarr test suite";
          };
          stop = {
            type = "app";
            program = "${tools.stop}/bin/theoriarr-stop";
            meta.description = "Stop Theoriarr";
          };
        }
      );

      # NixOS module: `services.theoriarr`.
      nixosModules.default =
        {
          config,
          lib,
          pkgs,
          ...
        }:
        let
          tools = mkTools pkgs;
          cfg = config.services.theoriarr;
        in
        {
          options.services.theoriarr = {
            enable = lib.mkEnableOption "Theoriarr";

            package = lib.mkOption {
              type = lib.types.package;
              default = tools.theoriarr;
              defaultText = lib.literalExpression "theoriarr.packages.\${system}.theoriarr";
              description = "The Theoriarr package to use.";
            };

            user = lib.mkOption {
              type = lib.types.str;
              default = "theoriarr";
              description = "User the service runs as.";
            };

            group = lib.mkOption {
              type = lib.types.str;
              default = "theoriarr";
              description = "Group the service runs as.";
            };

            port = lib.mkOption {
              type = lib.types.port;
              default = 6868;
              description = "Port the unified UI + API listener binds to.";
            };

            openFirewall = lib.mkOption {
              type = lib.types.bool;
              default = false;
              description = "Open the listen port in the firewall.";
            };

            disableAuth = lib.mkOption {
              type = lib.types.bool;
              default = false;
              description = "Disable the login page (API-key auth is unaffected).";
            };

            environmentFile = lib.mkOption {
              type = lib.types.nullOr lib.types.path;
              default = null;
              example = "/run/secrets/theoriarr.env";
              description = "Optional runtime environment file (for example secrets).";
            };
          };

          config = lib.mkIf cfg.enable {
            users.users.${cfg.user} = {
              isSystemUser = true;
              group = cfg.group;
              home = "/var/lib/theoriarr";
            };
            users.groups.${cfg.group} = { };

            systemd.services.theoriarr = {
              description = "Theoriarr unified Sonarr/Radarr backend";
              wantedBy = [ "multi-user.target" ];
              wants = [ "network-online.target" ];
              after = [ "network-online.target" ];
              # ffmpeg/VA-API for media compression (resolved from PATH).
              path = [
                pkgs.ffmpeg
                pkgs.libva-utils
              ];

              serviceConfig = {
                ExecStart = "${tools.dotnet}/bin/dotnet ${cfg.package}/src/Theoriarr.Series/_output/net10.0/Sonarr.dll --data /var/lib/theoriarr --nobrowser";
                User = cfg.user;
                Group = cfg.group;
                StateDirectory = "theoriarr";
                WorkingDirectory = "/var/lib/theoriarr";
                Environment = [
                  "THEORIARR_DATA=/var/lib/theoriarr"
                  "THEORIARR_PORT=${toString cfg.port}"
                  "Theoriarr__Server__Port=${toString cfg.port}"
                  "Theoriarr__Server__UrlBase="
                  "THEORIARR_TRANSCODE_TEMP_FOLDER=/var/lib/theoriarr/transcode"
                  "THEORIARR_DISABLE_AUTH=${if cfg.disableAuth then "true" else "false"}"
                ];
                EnvironmentFile = lib.optional (cfg.environmentFile != null) cfg.environmentFile;
                Restart = "on-failure";
                RestartSec = 5;

                NoNewPrivileges = true;
                ProtectSystem = "strict";
                ProtectHome = true;
                PrivateTmp = true;
                ProtectKernelTunables = true;
                ProtectControlGroups = true;
                RestrictAddressFamilies = [
                  "AF_INET"
                  "AF_INET6"
                  "AF_UNIX"
                ];
                ReadWritePaths = [ "/var/lib/theoriarr" ];
                CapabilityBoundingSet = "";
                AmbientCapabilities = "";
              };
            };

            networking.firewall.allowedTCPPorts = lib.optional cfg.openFirewall cfg.port;
          };
        };

      formatter = forAllSystems (system: pkgs: pkgs.nixfmt);
    };
}
