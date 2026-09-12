# Privacy & data

Effective 12 September 2026.

## Who publishes this app

**Eliaszac**, Denmark, publishes this independent desktop frontend. Contact [eliaszacho@gmail.com](mailto:eliaszacho@gmail.com) about privacy or support. This notice covers this project's application and information voluntarily shared with its maintainer. It does not replace the policies of your selected model providers, integrations or employer.

## What stays on your system

There is no publisher-operated account, telemetry collector or automatic conversation/crash upload in this application. The publisher cannot read your local conversations merely because you use the app.

- **App records:** normally under `%LOCALAPPDATA%\PiAgentGui`. These include project registrations and paths, saved Pi sessions, preferences, local diagnostics, extension state and management records. Conversation sessions can contain prompts, model responses, tool output, file contents, screenshots and other attachments.
- **Research:** app-managed background research has separate task records, results and worker sessions. It uses the configured model and tools when enabled.
- **Restore snapshots:** Workspace checkpoints stores data on the relevant execution target, normally under that user's `~/.pi-desktop-checkpoints`. Snapshots can contain copies of workspace files. Retention and clearing are managed by that extension; deleting a conversation schedules its associated snapshot cleanup.
- **Shared Pi data:** Pi configuration, installed packages and provider authentication normally live in `~/.pi/agent`, or the directory configured through `PI_CODING_AGENT_DIR`. These can also be used by Pi outside this frontend.
- **Credentials:** SSH and GitHub credentials use Windows credential facilities where implemented. Pi and integrations manage their own authentication storage; do not assume every configuration file is encrypted.
- **Diagnostics:** the app may write local exception types and stack traces for troubleshooting. They are not automatically sent to the publisher.

Session and settings files are not encrypted by the app. Their protection depends on operating-system access controls, disk protection and your backups. WSL/SSH execution and restore data may reside on the chosen target. Cloud-synced folders and backups are controlled by your own setup.

## Local usage summaries

Home derives token and model summaries from this app's saved Pi session records. It uses a temporary memory cache, not a separate analytics database or upload service. Counts are not account-wide usage or invoices.

Settings can turn off the Home usage reader. Pi still writes its own session usage metadata. Reset usage totals stores a local cutoff time and omits earlier responses from the display; it does not erase their session records or change a provider's records. Deleting a conversation removes its contribution after refresh, except history retained in another saved fork.

## What can leave your system

Configured providers receive requests needed to generate responses, which can include prompts, conversation context, selected images and files, tool results and relevant workspace content. Agent tools, extensions and MCP servers may communicate with further destinations. Remote execution sends commands and relevant data to the selected host. Local execution can also make network requests.

Provider/model discovery, sign-in, GitHub features, package installation, MCP connections and integration status checks can contact their respective services. Some refreshes happen automatically after those features are configured. Installing Pi or extensions uses their distribution services. Requests expose ordinary connection information, such as the source IP address, to the destination. This app does not route those requests through a publisher backend.

Each service may retain or process information under its own policies, including outside Denmark or the EEA. Review the service and account settings before sending sensitive data. This frontend cannot promise a provider's retention, training policy or transfer safeguards.

Relevant policies include [OpenAI](https://openai.com/policies/privacy-policy/), [Anthropic](https://www.anthropic.com/legal/privacy), [Google](https://policies.google.com/privacy), [GitHub](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement) and [Microsoft](https://privacy.microsoft.com/privacystatement). Other configured providers and integrations have their own notices.

## Retention and deletion

Saved conversations and project registrations remain until you remove them. Settling a conversation does not delete it. The app provides separate controls so you can understand what will be removed:

- **Delete all conversations:** removes catalog-owned conversations and schedules deletion of their session files, session screenshots and associated restore data. Project folders and shared Pi configuration remain.
- **Remove all projects:** also removes project registrations and their conversations. It does not delete the registered workspace directories or their files.
- **Workspace checkpoints → Manage:** manages snapshot storage separately from conversation history.
- **Reset usage totals:** changes the display cutoff only.

Active work must finish before bulk conversation deletion. Cleanup can be delayed by active resources, pending restore recovery or an unavailable remote host, and is retried on a later catalog load. The app reports pending cleanup. Separate research records, diagnostic files, preferences, installed extensions and shared provider credentials are not erased by deleting conversations. Uninstalling does not necessarily remove retained app data, Pi data, backups or remote copies.

Deleting local data does not delete information already sent to providers or saved by other systems. Use their controls or contact them for those records. The maintainer cannot remotely erase files from your computer.

## Support and public contributions

If you send email, the maintainer receives your email address and whatever you include, for the purpose of responding and maintaining the project. Public GitHub issues and contributions are visible to other people and are processed by GitHub under its own policy. Do not submit secrets, private prompts or confidential files; redact diagnostic attachments before sharing them.

Where data-protection law applies to the maintainer's handling of support correspondence, the basis is the legitimate interest in answering requests and maintaining the software. Correspondence is retained while needed to resolve the request, track relevant issues or meet legal obligations; unnecessary personal details should be removed when no longer needed. GitHub contributions and issue history may remain public as part of the project's development record. Email and repository hosting providers also process the information through their services.

## Your rights and contact

For personal information the maintainer actually holds, contact the email above to request access, correction, deletion or restriction, or object to processing. Portability and other rights apply where their legal conditions are met. Identity may need to be verified proportionately. These rights do not give the maintainer access to local or provider-held records it does not possess.

You may complain to your competent supervisory authority, including [Datatilsynet, the Danish Data Protection Agency](https://www.datatilsynet.dk/english/file-a-complaint). The authority recommends contacting the person or organisation involved first. This does not remove your right to complain.
