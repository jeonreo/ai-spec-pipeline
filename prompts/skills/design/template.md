<!DOCTYPE html>
<html lang="ko">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>[페이지 타이틀]</title>
  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&display=swap" rel="stylesheet">
  <style>
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: 'Inter', sans-serif; font-size: 14px; background: #f4f6fb; color: #202020; }

    /* SNB */
    .snb { position: fixed; left: 0; top: 0; width: 210px; height: 100vh; background: #24283a; display: flex; flex-direction: column; }
    .snb-header { padding: 28px 20px; color: #fff; font-size: 15px; font-weight: 600; letter-spacing: 0.5px; }
    .snb-item { display: flex; align-items: center; gap: 10px; height: 44px; padding: 0 16px; color: #d9d9d9; font-size: 14px; border-radius: 8px; margin: 2px 8px; cursor: pointer; text-decoration: none; }
    .snb-item:hover { background: rgba(255,255,255,.08); }
    .snb-item.active { background: rgba(255,255,255,.12); color: #fff; }
    .snb-icon { font-size: 16px; width: 18px; display: inline-block; text-align: center; opacity: .7; }
    .snb-item.active .snb-icon, .snb-item:hover .snb-icon { opacity: 1; }

    /* Header */
    .page-header { position: sticky; top: 0; z-index: 10; margin-left: 210px; height: 58px; background: #fff; box-shadow: 0 2px 10px rgba(0,0,0,.04); display: flex; align-items: center; justify-content: space-between; padding: 0 24px; }
    .page-title { font-size: 14px; font-weight: 600; text-transform: uppercase; letter-spacing: 1px; }

    /* Content */
    .content { margin-left: 210px; min-height: 100vh; padding: 24px; }
    .section-label { display: block; font-size: 12px; font-weight: 600; color: #5b6dc0; text-transform: uppercase; letter-spacing: 1.2px; margin-bottom: 12px; }

    /* Card */
    .card { background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; padding: 20px; box-shadow: 0 2px 10px rgba(0,0,0,.04); margin-bottom: 24px; }
    .card h3 { font-size: 16px; font-weight: 600; margin: 8px 0 10px; }
    .card p { color: #7d7d7d; line-height: 1.6; margin-bottom: 16px; }

    /* Badges */
    .badge-blue { background: #eef0fc; color: #5b6dc0; border-radius: 4px; padding: 2px 8px; font-size: 11px; font-weight: 600; text-transform: uppercase; }
    .badge-gray { background: #f0f0f2; color: #7d7d7d; border-radius: 4px; padding: 2px 8px; font-size: 11px; font-weight: 500; }

    /* Buttons */
    .btn-primary { background: #5b6dc0; color: #fff; border: none; border-radius: 6px; padding: 8px 16px; font-size: 14px; font-weight: 600; cursor: pointer; box-shadow: 0 2px 6px rgba(91,109,192,.3); }
    .btn-outline { background: transparent; color: #5b6dc0; border: 1px solid #5b6dc0; border-radius: 6px; padding: 6px 14px; font-size: 13px; font-weight: 500; cursor: pointer; }

    /* Table */
    .table-toolbar { display: flex; align-items: center; margin-bottom: 12px; }
    .table-toolbar input { border: 1px solid #e5e7eb; border-radius: 6px; padding: 7px 12px; font-size: 13px; width: 240px; outline: none; }
    .table-toolbar input:focus { border-color: #5b6dc0; }
    table { width: 100%; border-collapse: collapse; background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; overflow: hidden; box-shadow: 0 2px 10px rgba(0,0,0,.04); }
    thead { background: #f8f9fc; }
    th { padding: 12px 16px; text-align: left; font-size: 12px; font-weight: 600; color: #7d7d7d; text-transform: uppercase; letter-spacing: 0.5px; border-bottom: 1px solid #e5e7eb; }
    td { padding: 12px 16px; font-size: 13px; border-bottom: 1px solid #f0f0f2; }
    tr:last-child td { border-bottom: none; }
    .pagination { display: flex; align-items: center; justify-content: flex-end; gap: 4px; margin-top: 12px; }
    .pagination button { border: 1px solid #e5e7eb; background: #fff; border-radius: 5px; width: 30px; height: 30px; font-size: 13px; cursor: pointer; color: #555; }
    .pagination button.active { background: #5b6dc0; color: #fff; border-color: #5b6dc0; }
  </style>
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
