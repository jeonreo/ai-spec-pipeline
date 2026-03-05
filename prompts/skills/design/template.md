<!DOCTYPE html>
<html lang="ko">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>[페이지 타이틀]</title>
  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&display=swap" rel="stylesheet">
  <!--STYLE-->
</head>
<body>
  <nav class="snb">
    <div class="snb-header">[서비스명]</div>
    <a class="snb-item active"><span class="snb-icon">⊞</span> Dashboard</a>
    <a class="snb-item"><span class="snb-icon">≡</span> Spec</a>
    <a class="snb-item"><span class="snb-icon">☑</span> Tasks</a>
    <a class="snb-item"><span class="snb-icon">◈</span> Design</a>
    <a class="snb-item"><span class="snb-icon">⚙</span> Settings</a>
  </nav>

  <header class="page-header">
    <span class="page-title">[페이지명]</span>
    <button class="btn-primary">[액션명]</button>
  </header>

  <main class="content">
    <span class="section-label">[섹션명]</span>
    <div class="card">
      <span class="badge-blue">[배지]</span>
      <h3>[카드 제목]</h3>
      <p>[카드 본문 — 기능 또는 현황 설명]</p>
      <button class="btn-outline">[버튼명]</button>
    </div>

    <span class="section-label">[섹션명]</span>
    <div class="table-toolbar">
      <input type="search" placeholder="[검색 플레이스홀더]">
    </div>
    <table>
      <thead>
        <tr>
          <th>[컬럼1]</th>
          <th>[컬럼2]</th>
          <th>[컬럼3]</th>
        </tr>
      </thead>
      <tbody>
        <tr><td>[값]</td><td>[값]</td><td><span class="badge-gray">[상태]</span></td></tr>
        <tr><td>[값]</td><td>[값]</td><td><span class="badge-blue">[상태]</span></td></tr>
        <tr><td>[값]</td><td>[값]</td><td><span class="badge-gray">[상태]</span></td></tr>
      </tbody>
    </table>
    <div class="pagination">
      <button>‹</button>
      <button class="active">1</button>
      <button>2</button>
      <button>3</button>
      <button>›</button>
    </div>
  </main>
</body>
</html>
