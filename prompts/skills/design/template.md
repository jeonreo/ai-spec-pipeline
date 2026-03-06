<!DOCTYPE html>
<html lang="ko">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>[페이지 타이틀]</title>
  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
  <!--STYLE-->
</head>
<body>

  <!-- 사이드바 내비게이션 -->
  <nav class="snb">
    <div class="snb-logo">
      <div class="snb-logo-icon">[서비스 이니셜]</div>
      <span class="snb-logo-name">[서비스명]</span>
    </div>

    <div class="snb-group">
      <div class="snb-group-label">[메뉴 그룹1]</div>
      <a class="snb-item active">[서브메뉴 활성]</a>
      <a class="snb-item">[서브메뉴2]</a>
    </div>
    <div class="snb-group">
      <div class="snb-group-label">[메뉴 그룹2]</div>
      <a class="snb-item">[서브메뉴]</a>
    </div>
    <div class="snb-group">
      <div class="snb-group-label">[메뉴 그룹3]</div>
      <a class="snb-item">[서브메뉴]</a>
    </div>
  </nav>

  <!-- 상단 바 (브레드크럼) -->
  <div class="topbar">
    <nav class="breadcrumb">
      <a class="breadcrumb-item">[최상위 메뉴]</a>
      <span class="breadcrumb-sep">›</span>
      <a class="breadcrumb-item">[상위 메뉴]</a>
      <span class="breadcrumb-sep">›</span>
      <span class="breadcrumb-item current">[현재 페이지]</span>
    </nav>
  </div>

  <div class="page-wrap">
    <div class="content">

      <!-- 좌측 상세 카드 + 우측 섹션 2컬럼 레이아웃 -->
      <div class="detail-layout">

        <!-- 좌측 프로필/상세 카드 -->
        <div>
          <div class="detail-card">
            <div class="detail-card-id">
              <span class="detail-card-id-text">[항목 ID 또는 이름]</span>
              <span class="detail-card-id-copy">⎘</span>
            </div>

            <div class="field-row">
              <span class="field-label">[필드명1]</span>
              <span class="field-value">
                [필드값1]
                <span class="badge badge-green">Verified</span>
              </span>
            </div>
            <div class="field-row">
              <span class="field-label">[필드명2]</span>
              <span class="field-value">[필드값2]</span>
            </div>
            <div class="field-row">
              <span class="field-label">[필드명3]</span>
              <span class="field-value">[필드값3]</span>
            </div>
            <div class="field-row">
              <span class="field-label">[필드명4]</span>
              <span class="field-value">
                <span class="badge badge-blue">[상태값]</span>
              </span>
            </div>
            <div class="field-row">
              <span class="field-label">[필드명5]</span>
              <span class="field-value">[필드값5]</span>
            </div>
            <div class="field-row">
              <span class="field-label">[필드명6]</span>
              <span class="field-value">[필드값6]</span>
            </div>

            <div class="detail-card-actions">
              <button class="btn-field">[액션 버튼1]</button>
              <button class="btn-field">[액션 버튼2]</button>
              <button class="btn-link">⧉ [링크 액션]</button>
            </div>
          </div>
        </div>

        <!-- 우측 섹션들 -->
        <div class="sections">

          <!-- 섹션1: 메타 정보 그리드 -->
          <div class="section-block">
            <div class="section-header">
              <span class="section-title">[섹션1 제목]</span>
              <div class="section-header-actions">
                <div class="toggle-wrap">
                  <span class="toggle-label">Off</span>
                  <label class="toggle"><input type="checkbox"><span class="toggle-slider"></span></label>
                  <button class="btn-edit">[액션명]</button>
                </div>
              </div>
            </div>
            <div class="info-grid">
              <div class="info-cell">
                <div class="info-cell-label">[컬럼1]</div>
                <div class="info-cell-value">[값1]</div>
              </div>
              <div class="info-cell">
                <div class="info-cell-label">[컬럼2]</div>
                <div class="info-cell-value">[값2]</div>
                <div class="info-cell-sub">[부가정보]</div>
              </div>
              <div class="info-cell">
                <div class="info-cell-label">[컬럼3]</div>
                <div class="info-cell-value">[값3]</div>
              </div>
              <div class="info-cell">
                <div class="info-cell-label">[컬럼4]</div>
                <div class="info-cell-value">[값4]</div>
              </div>
            </div>
          </div>

          <!-- 섹션2: 구독/라이선스 카드 -->
          <div class="section-block">
            <div class="section-header">
              <span class="section-title">[섹션2 제목]</span>
              <div class="section-header-actions">
                <div class="toggle-wrap">
                  <span class="toggle-label">Off</span>
                  <label class="toggle"><input type="checkbox"><span class="toggle-slider"></span></label>
                </div>
                <button class="btn-edit">[편집 액션]</button>
              </div>
            </div>
            <div class="sub-card">
              <div class="sub-card-header">
                <div class="sub-card-title">
                  <div class="sub-card-icon">≡</div>
                  [구독/항목 타이틀]
                </div>
                <a class="btn-edit">↗ [편집 링크]</a>
              </div>
              <div class="sub-meta-grid">
                <div class="sub-meta-cell">
                  <div class="sub-meta-label">[메타1]</div>
                  <div class="sub-meta-value">[값]</div>
                </div>
                <div class="sub-meta-cell">
                  <div class="sub-meta-label">[메타2]</div>
                  <div class="sub-meta-value">
                    [타입] <span class="badge badge-gray">[배지]</span>
                  </div>
                </div>
                <div class="sub-meta-cell">
                  <div class="sub-meta-label">[메타3]</div>
                  <div class="sub-meta-value">1</div>
                </div>
                <div class="sub-meta-cell">
                  <div class="sub-meta-label">[종료일]</div>
                  <div class="sub-meta-value">[날짜]</div>
                </div>
                <div class="sub-meta-cell">
                  <div class="sub-meta-label">[등록일]</div>
                  <div class="sub-meta-value">[날짜]</div>
                </div>
                <div class="sub-meta-cell">
                  <div class="sub-meta-label">[주기]</div>
                  <div class="sub-meta-value">[값]</div>
                </div>
                <div class="sub-meta-cell">
                  <div class="sub-meta-label">[상태]</div>
                  <div class="sub-meta-value"><span class="badge badge-green">[상태값]</span></div>
                </div>
                <div class="sub-meta-cell">
                  <div class="sub-meta-label">[액션]</div>
                  <div class="sub-meta-value"><a class="td-link">[링크]</a></div>
                </div>
              </div>
              <div class="sub-detail">
                <div class="sub-detail-name">[소프트웨어/항목명]</div>
                <div class="sub-detail-desc">
                  • [설명 항목1]<br>
                  • [설명 항목2]
                </div>
              </div>
              <div class="sub-actions">
                <button class="btn-arrow">[액션1] →</button>
                <span class="btn-arrow-sep"></span>
                <button class="btn-arrow">[액션2] →</button>
                <span class="btn-arrow-sep"></span>
                <button class="btn-arrow">[액션3] →</button>
              </div>
            </div>
          </div>

          <!-- 섹션3: 탭 + 상세 테이블 -->
          <div class="section-block">
            <div class="section-header">
              <span class="section-title">[섹션3 제목]</span>
            </div>
            <div class="tab-nav">
              <div class="tab-item active">[탭1]</div>
              <div class="tab-item">[탭2]</div>
              <div class="tab-item">[탭3]</div>
              <div class="tab-item">[탭4]</div>
            </div>
            <div class="tab-content">
              <table class="detail-table">
                <tbody>
                  <tr>
                    <th>[필드명]</th>
                    <td>[값]</td>
                  </tr>
                  <tr>
                    <th>[필드명]</th>
                    <td>[값]</td>
                  </tr>
                  <tr>
                    <th>[필드명]</th>
                    <td>[값]</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

        </div>
      </div>

    </div>
  </div>

</body>
</html>
