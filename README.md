# AI Spec Pipeline

AI Spec Pipeline turns an initial request into a staged delivery flow:

- `intake`: clarify the request and scope
- `spec`: produce the detailed implementation spec
- `jira`: produce a human-friendly Jira ticket draft

The project now supports two entry points:

- Web console for manual pipeline runs, history, settings, and debugging
- Slack DM workflow for staged approval, review, and Jira creation

## Current Slack Workflow

The current Slack-native flow is:

1. User runs `/spec <request>`
2. The bot opens or reuses a DM with that user
3. The workflow runs through `intake -> spec -> jira`
4. Each stage uses the same pattern:
   - Worker draft
   - Reviewer feedback
   - User approval or revision
5. Jira is created only after the user presses `Create Jira`

Important behavior:

- `intake`, `spec`, and `jira` each have an automated reviewer
- `Request changes` puts the stage into waiting state
- The next user reply in the same stage thread is treated as revision feedback
- `Change Jira Settings` updates the current workflow and also saves the user's personal Jira default for future workflows
- `spec.md` is automatically attached when the Jira issue is created

## Architecture

### Core backend pieces

- `StageExecutionService`
  - Shared execution entry point for web and Slack flows
- `SlackBotService`
  - Slack API calls such as DM open, message post/update, modal open/update
- `SlackSocketModeService`
  - Socket Mode connection for slash commands, interactive actions, and DM message events
- `SlackWorkflowService`
  - Workflow orchestration, stage transitions, reviewer flow, persistence, and Jira creation
- `WorkflowUserPreferencesService`
  - Per-user Jira defaults keyed by Slack user id

### Workflow persistence

Each Slack workflow is stored in its workspace:

```text
workspaces/local/{date}-{id}/
  workflow.json
  slack.json
  events.jsonl
  input.txt
  out/
    intake.md
    intake-review.json
    spec.md
    spec-review.json
    jira.json
    jira-review.json
```

## Prompt Structure

Prompts are organized by role and stage:

```text
prompts/
  agents/
  context/
  skills/
    intake/
    intake-review/
    spec/
    spec-review/
    jira/
    jira-review/
```

Design intent:

- `intake` and `spec` are detailed, agent-facing documents
- `jira` is optimized for humans scanning title, description, and acceptance criteria
- Reviewer prompts share the same JSON contract:
  - `decision`
  - `summary`
  - `strengths`
  - `issues`
  - `recommended_changes`

## Environment Setup

Secrets should live in `.env`, not in `appsettings.json`.

Create a root `.env` file from [`.env.example`](/d:/dev/ai-spec-pipeline/.env.example).

Recommended values:

```env
Jira__ApiToken=your_jira_api_token
GitHub__Token=your_github_token
Slack__BotToken=xoxb-your-slack-bot-token
Slack__AppToken=xapp-your-slack-app-token

# Optional
Slack__SigningSecret=
App__PublicBaseUrl=
```

Notes:

- `Slack__AppToken` is required for Socket Mode
- `Slack__SigningSecret` is only needed for HTTP webhook fallback
- `App__PublicBaseUrl` is optional in Socket Mode
  - if it is set, Slack messages can link back to the web console

Non-secret Jira defaults still live in [appsettings.json](/d:/dev/ai-spec-pipeline/backend/LocalCliRunner.Api/appsettings.json):

```json
{
  "Jira": {
    "BaseUrl": "https://your-org.atlassian.net",
    "Email": "you@example.com",
    "DefaultProjectKey": "PROJ",
    "DefaultIssueTypeName": "Story"
  }
}
```

## Slack App Setup

This project is designed to use Slack Socket Mode for local development, so a public tunnel is not required.

### Required Slack features

- Socket Mode: `On`
- Interactivity: `On`
- Event Subscriptions: `On`
- App Home > Messages Tab: allow messages and slash commands from the messages tab

### App-level token

Create an app-level token with:

- `connections:write`

Store it in `.env` as:

- `Slack__AppToken`

### Bot scopes

Required minimum bot scopes:

- `commands`
- `chat:write`
- `im:read`
- `im:history`

Helpful additional scope if your workspace requires it:

- `im:write`

### Bot events

Required bot event:

- `message.im`

### Slash commands

Register these slash commands in Slack:

- `/spec`
  - Short Description: `Start a staged workflow in DM`
- `/spec-status`
  - Short Description: `Show your latest workflow status`
- `/spec-rerun`
  - Short Description: `Rerun the current or selected stage`
- `/spec-help`
  - Short Description: `Show available workflow commands`

For Socket Mode apps, a public Request URL is not required for these commands.

## Local Development

### Install dependencies

```powershell
npm install
```

### Run the app

Windows helper:

```powershell
.\run.win.ps1
```

Or run services manually:

```powershell
dotnet run --project backend\LocalCliRunner.Api\LocalCliRunner.Api.csproj
npm run dev
```

Default local URLs:

- Frontend: `http://127.0.0.1:5173`
- Backend: `http://127.0.0.1:5001`

## How To Test

### Slack workflow test

1. Start the backend and frontend
2. Confirm the backend log shows Socket Mode connection success
3. In Slack, run:

```text
/spec Add user email search for admins
```

4. Confirm the bot opens or reuses a DM
5. Confirm an `Intake Agent` thread appears
6. Confirm the flow for each stage:
   - draft message
   - reviewer message
   - approval buttons
7. Use `Request changes` and reply in the same thread
8. Approve `intake` and `spec`
9. Confirm `Jira Agent` produces a clean title and description
10. Use `Change Jira Settings` to switch project or issue type
11. Press `Create Jira`
12. Confirm:
   - the issue is created
   - the issue link is posted back to Slack
   - `spec.md` is attached

### Web console test

1. Open `http://127.0.0.1:5173`
2. Open the `Slack Workflows` panel
3. Confirm you can:
   - list workflows
   - inspect stage status
   - inspect stored stage output previews
   - rerun the current or selected stage

## Jira Defaults Per PM

Different PMs may work against different Jira projects or issue types.

Current behavior:

- The first workflow starts with app-level Jira defaults
- If the PM changes Jira settings in Slack, those values are saved per Slack user
- Future workflows for that same user reuse the saved project and issue type

Persistence file:

- [workflow-user-preferences.json](/d:/dev/ai-spec-pipeline/backend/LocalCliRunner.Api/workflow-user-preferences.json)

This file is intentionally ignored by git.

## Build Verification

Useful local checks:

```powershell
dotnet build backend\LocalCliRunner.Api\LocalCliRunner.Api.csproj
npm run build
```

## Main Files

- [Program.cs](/d:/dev/ai-spec-pipeline/backend/LocalCliRunner.Api/Program.cs)
- [SlackWorkflowService.cs](/d:/dev/ai-spec-pipeline/backend/LocalCliRunner.Api/Infrastructure/SlackWorkflowService.cs)
- [SlackSocketModeService.cs](/d:/dev/ai-spec-pipeline/backend/LocalCliRunner.Api/Infrastructure/SlackSocketModeService.cs)
- [StageExecutionService.cs](/d:/dev/ai-spec-pipeline/backend/LocalCliRunner.Api/Infrastructure/StageExecutionService.cs)
- [WorkflowUserPreferencesService.cs](/d:/dev/ai-spec-pipeline/backend/LocalCliRunner.Api/Infrastructure/WorkflowUserPreferencesService.cs)
- [WorkflowController.cs](/d:/dev/ai-spec-pipeline/backend/LocalCliRunner.Api/Controllers/WorkflowController.cs)
- [SlackWorkflowController.cs](/d:/dev/ai-spec-pipeline/backend/LocalCliRunner.Api/Controllers/SlackWorkflowController.cs)
- [WorkflowPanel.tsx](/d:/dev/ai-spec-pipeline/web/src/components/WorkflowPanel.tsx)

## Current Limits

Slack DM workflow currently covers:

- `intake`
- `spec`
- `jira`

Later stages such as QA, design, code analysis, patch generation, and PR creation are still web-first and are not yet part of the Slack-native v1 flow.
