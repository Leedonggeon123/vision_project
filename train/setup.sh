#!/usr/bin/env bash
# 서버(WSL/리눅스)에서 한 번만 실행: bash setup.sh
# 학습 환경(가상환경 + PyTorch + anomalib)을 만든다.
# 주의: 서버 PC에서 처음 돌리는 것이라 오류가 날 수 있다. 오류 메시지를 그대로 알려줄 것.
set -e

echo "[1/5] Python 확인"
python3 --version

echo "[2/5] 가상환경 만들기 (~/venv_anomalib)"
python3 -m venv ~/venv_anomalib

echo "[3/5] 가상환경 켜기"
source ~/venv_anomalib/bin/activate
pip install --upgrade pip

echo "[4/5] PyTorch 설치 (RTX 5090은 CUDA 12.8 이상용 빌드 필요)"
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu128

echo "[5/5] anomalib 설치"
pip install anomalib

echo "---- 확인 ----"
python -c "import torch; print('torch', torch.__version__); print('cuda 사용 가능:', torch.cuda.is_available()); print(torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'GPU 없음')"
pip show anomalib | head -n 3
echo "완료. 위 출력의 anomalib Version 을 기록해 둘 것."
