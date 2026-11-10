"""PatchCore 학습 (MVTec AD metal_nut).

사용법 (가상환경을 켠 상태에서):
    python train.py                 # 데이터 위치 기본값: ~/mvtec
    python train.py /경로/mvtec     # 데이터 위치 직접 지정

데이터 폴더 구조: <root>/metal_nut/{train,test,ground_truth}
주의: anomalib 버전에 따라 이름이 다를 수 있다 (MVTecAD / MVTec).
      오류가 나면 메시지를 그대로 알려줄 것.

수정 이력: Python 3.14 에서 DataLoader 워커가 새 프로세스를 띄울 때
           train.py 가 다시 실행되어 RuntimeError 가 나는 문제가 있었다.
           -> if __name__ == "__main__" 로 감싸고, num_workers=0 으로 설정.
"""
import glob
import os
import shutil
import sys
from pathlib import Path

try:
    from anomalib.data import MVTecAD as MVTec  # 새 버전
except ImportError:
    from anomalib.data import MVTec  # 이전 버전

from anomalib.engine import Engine
from anomalib.models import Patchcore


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else str(Path.home() / "mvtec")
    print("데이터 위치:", root)

    # num_workers=0: 사진을 읽는 별도 프로세스를 만들지 않는다 (WSL/Python 3.14 오류 방지)
    datamodule = MVTec(root=root, category="metal_nut", num_workers=0)
    model = Patchcore()
    engine = Engine()

    # 1) 정상 사진(train/good)을 보여 줘서 정상 조각을 저장 = 학습
    engine.fit(model=model, datamodule=datamodule)

    # 2) test 사진(정상 + 불량 4종)으로 평가
    engine.test(model=model, datamodule=datamodule)

    # 3) 모델 파일(.ckpt) 찾아서 한곳에 복사
    ckpts = sorted(
        glob.glob(os.path.expanduser("~/**/*.ckpt"), recursive=True),
        key=os.path.getmtime,
    )
    if ckpts:
        out_dir = Path.home() / "model_out"
        out_dir.mkdir(exist_ok=True)
        newest = ckpts[-1]
        shutil.copy(newest, out_dir / "patchcore_metal_nut.ckpt")
        print("모델 파일:", newest)
        print("복사본:", out_dir / "patchcore_metal_nut.ckpt")
    else:
        print(".ckpt 파일을 찾지 못했다. results 폴더를 직접 확인할 것.")


if __name__ == "__main__":
    main()
