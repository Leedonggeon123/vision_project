# data/

이 폴더에 MVTec AD의 `metal_nut` 데이터를 둡니다. (시연·시험·재학습용)

```
data/mvtec_metal_nut/metal_nut/
  train/good/        정상 220장 (학습)
  test/good/         정상 22장 (평가)
  test/bent|color|flip|scratch/   불량 93장 (평가)
  ground_truth/      불량 위치 정답 그림
```

## 내려받는 곳

- 공식: <https://www.mvtec.com/company/research/datasets/mvtec-ad>
- Hugging Face 복사본: <https://huggingface.co/datasets/foersben/mvtec-ad> (`metal_nut` 폴더만 약 166MB)

```python
from huggingface_hub import snapshot_download
snapshot_download(repo_id="foersben/mvtec-ad", repo_type="dataset",
                  allow_patterns="metal_nut/*", local_dir="data/mvtec_metal_nut")
```

## 라이선스

MVTec AD는 **CC BY-NC-SA 4.0** (비상업적 사용, 출처 표기, 동일 조건 공유)입니다. 그래서 데이터와 이 데이터로 만든 시연 영상은 Git 저장소에 포함하지 않았습니다.

> Bergmann et al., *MVTec AD — A Comprehensive Real-World Dataset for Unsupervised Anomaly Detection*, CVPR 2019.
