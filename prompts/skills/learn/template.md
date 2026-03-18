{
  "suggestions": [
    {
      "stage": "intake",
      "issue": "Q&A 섹션에 구체적인 질문이 부족하고 Yes/No 형태로 추출됨",
      "suggestion": "결정 필요 항목을 수치·범위·조건을 요구하는 형태로 추출",
      "skill_patch": "## 추가 지침\n- Q. 항목은 Yes/No가 아닌 구체적 수치, 범위, 조건을 요구하는 형태로 작성한다\n- 예: \"페이지네이션 크기는 몇 개인가요?\" (O), \"페이지네이션이 필요한가요?\" (X)"
    },
    {
      "stage": "spec",
      "issue": "BE API 경로와 요청/응답 타입이 미명시",
      "suggestion": "API 경로, HTTP 메서드, 요청/응답 타입을 항상 명시하도록 강화",
      "skill_patch": "## 추가 지침\n- BE 섹션에 API 경로, HTTP 메서드, 요청 파라미터, 응답 타입을 명시한다\n- 예: GET /api/v1/users?email=&offset=&limit= → UserPageResponse"
    }
  ]
}
