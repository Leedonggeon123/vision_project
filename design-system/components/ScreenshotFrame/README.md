화면 스크린샷을 감싸는 테두리 카드입니다. 번호 배지의 기준 틀이기도 합니다.

**제공할 것:** 스크린샷 이미지(너비 100%, 비율 유지), 그 위에 올릴 `NumberBadge`들.

- `surface` 바탕, `border` 1px, `radius-lg`, `overflow: hidden`, `position: relative`.
- 이미지는 `display:block; width:100%; height:auto`. 크게 키우지도 자르지도 않습니다.
- 프레임 아래 `space-16`~18px 떨어뜨려 `LegendRow` 격자를 둡니다.
- 이미지에는 `alt`로 화면 이름을 씁니다.

**하지 않기:** 그림자, 이중 테두리, 브라우저 창 장식.
