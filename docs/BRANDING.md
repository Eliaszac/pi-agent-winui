# Branding

Current artwork and distribution decisions. Artwork corrections were accepted by the user on 13 September 2026. Asset provenance lives beside the files; shipped attribution is in [third-party notices](../Legal/THIRD-PARTY-NOTICES.md).

## Current artwork

- **Application:** original conversation/code artwork in `Assets/PiLogo.svg` and `Assets/Pi.ico`, under the root MIT license. It replaces the upstream Pi badge. Existing filenames remain for executable, window and installer compatibility.
- **GitHub:** official black/white Invertocat identifies the GitHub feature and quick integration. See [asset provenance](../Assets/OpenIn/README.md) and [brand guidance](https://brand.github.com/foundations/logo).
- **VS Code and Windsurf:** official artwork identifies their Open in actions. See [VS Code guidance](https://code.visualstudio.com/brand), [Windsurf guidance](https://windsurf.com/brand), and [asset provenance](../Assets/OpenIn/README.md).
- **Linear:** unchanged official light/dark logomarks identify the quick integration. See [asset provenance](../Assets/Integrations/README.md) and [brand guidance](https://linear.app/brand).
- **Other providers, editors and integrations:** neutral symbols beside product names. The former Lobe/Simple Icons artwork and superseded editor marks are removed. Neutral symbols avoid relying on unverified permission or fitting artwork into controls below its required size.
- **File-type and syntax assets:** retain their existing licenses, including the separate Visual Studio Image Library EULA. These are distinct from the removed application logos.

Keep retained third-party artwork unchanged, secondary to this application's identity, and used only to identify the corresponding product or integration. A collection's copyright license does not by itself grant underlying trademark rights. No owner has been contacted and no individual permission has been obtained on the project's behalf.

## Release decisions

The confirmed public name is **Pi desktop**, described as an independent frontend for Pi. The confirmed publisher/contact is **Eliaszac, Denmark**, **eliaszacho@gmail.com**. Installer publisher and application company metadata use **Eliaszac**; the About page and legal documents also state Denmark and the contact address. This is the supplied public publisher identity, not an inferred legal name or verified signing identity.

Keep `PiAgentGui.Desktop`, the executable/resource names, installation directory and user-data paths unchanged for upgrade compatibility. The application ships unpackaged through Inno Setup; the unused MSIX template manifest is not a public-distribution or signing identity. Code signing remains separate.

Fresh builds exclude removed artwork. Upgrades explicitly remove the 63 obsolete artwork files deleted in commit `dcbdd3e`, using the exact-path inventory in `Installer/ObsoleteArtwork.iss`. Current replacement artwork and dependency notices are retained; see [installer guidance](../Installer/README.md). Keep required dependency notices when changing packaging. User acceptance and build checks do not establish blanket trademark permission.
