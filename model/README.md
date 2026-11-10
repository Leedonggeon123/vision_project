# model/

이 폴더에 학습된 모델 파일을 넣습니다.

```
model/patchcore_metal_nut.ckpt     (약 227MB)
```

- 용량이 GitHub 파일 크기 제한(100MB)을 넘어서 **Git 저장소에는 포함하지 않았습니다.**
- 모델은 MVTec AD metal_nut의 정상 사진 220장으로 PatchCore(anomalib 2.7.0)를 학습한 결과입니다.
- 직접 만들려면 `train/` 폴더의 `train.py`로 학습하면 `~/model_out/patchcore_metal_nut.ckpt`가 생깁니다. (루트 README의 '학습' 참고)
- 학습 서버(GPU)에서 저장한 모델은 검사 엔진이 읽을 때 CPU로 자동 지정합니다.
- 큰 파일을 Git으로 꼭 관리하려면 [Git LFS](https://git-lfs.com/)를 쓰거나, GitHub Releases·드라이브 링크로 따로 공유하세요.
