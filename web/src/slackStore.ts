import type { SlackFile } from './api'

// 모듈 수준 단순 스토어 — Slack 추출 파일을 세션 동안 보관
// SourcePanel(쓰기) ↔ JiraView(읽기) 간 prop drilling 없이 공유
let _files: SlackFile[] = []

export const slackStore = {
  set(files: SlackFile[]) { _files = files },
  get(): SlackFile[]      { return _files  },
  clear()                 { _files = []    },
  count()                 { return _files.length },
}
