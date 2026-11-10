"""PatchCore 검사 엔진 (WSL의 Python 에서 실행).

WPF 가 이 프로그램을 한 번 켜 두고, 사진 경로를 한 줄씩 보내면
결과를 JSON 한 줄로 돌려준다. (모델은 켤 때 한 번만 불러온다)

사용법 (WSL, 가상환경 ~/venv_infer 를 켠 상태):
    # 1) 켜 두고 쓰는 방식 (WPF 연동용)
    python infer_engine.py --serve
        시작하면  {"status":"ready", ...}  한 줄을 출력한다.
        그 뒤 stdin 으로 사진 경로를 한 줄씩 보내면 JSON 한 줄이 돌아온다.
        "quit" 를 보내면 종료.

    # 2) 한 장만 검사
    python infer_engine.py 사진경로.png

경로는 윈도우 형식(C:\\...)과 WSL 형식(/mnt/c/...) 모두 받는다.
결과의 heatmap_path 는 입력과 같은 형식으로 돌려준다.

출력 JSON 예:
    {"ok": true, "result": "NG", "score": 0.9348, "threshold": 0.5,
     "image_path": "...", "heatmap_path": "...", "elapsed_ms": 812}
"""
import argparse
import json
import os
import sys
import time
import warnings
from pathlib import Path

warnings.filterwarnings("ignore")

import tempfile

IS_WIN = os.name == "nt"
# 이 파일(engine/infer_engine.py) 기준으로 프로젝트 폴더를 찾는다 (윈도우/WSL 공통)
PROJECT = str(Path(__file__).resolve().parent.parent)
DEFAULT_CKPT = str(Path(PROJECT) / "model" / "patchcore_metal_nut.ckpt")
DEFAULT_OUT = str(Path(PROJECT) / "results")


# ---------- 경로 변환 (윈도우 <-> WSL) ----------
def to_linux(p: str) -> str:
    """현재 실행 환경에서 열 수 있는 경로로 바꾼다.
    - WSL(리눅스): C:\\... -> /mnt/c/...
    - 윈도우: /mnt/c/... -> C:\\...  (윈도우 경로는 그대로)
    (함수 이름은 예전 그대로 두었지만 두 환경 모두에서 쓴다)"""
    p = p.strip().strip('"')
    if IS_WIN:
        if p.startswith("/mnt/") and len(p) > 6 and p[6] == "/":
            return p[5].upper() + ":" + p[6:].replace("/", "\\")
        return p
    if len(p) > 2 and p[1] == ":" and p[2] in "\\/":
        return "/mnt/" + p[0].lower() + p[2:].replace("\\", "/")
    return p


def to_windows(p: str) -> str:
    if p.startswith("/mnt/") and len(p) > 6 and p[6] == "/":
        return p[5].upper() + ":" + p[6:].replace("/", "\\")
    return p


class Engine2:
    def __init__(self, ckpt: str, out_dir: str, threshold: float | None):
        import torch
        from anomalib.engine import Engine
        from anomalib.models import Patchcore

        self.torch = torch
        self.out_dir = Path(out_dir)
        self.out_dir.mkdir(parents=True, exist_ok=True)
        self.threshold = threshold

        use_gpu = torch.cuda.is_available()
        # GPU 로 학습한 파일을 CPU 만 있는 PC 에서도 열 수 있도록 map_location 지정
        # (직접 학습한 신뢰할 수 있는 파일이므로 weights_only=False)
        self.model = Patchcore.load_from_checkpoint(
            ckpt, map_location="cuda" if use_gpu else "cpu", weights_only=False
        )
        self.model.eval()
        self.engine = Engine(
            accelerator="gpu" if use_gpu else "cpu",
            default_root_dir=str(Path(tempfile.gettempdir()) / "infer_engine_out"),
            logger=False,
            enable_progress_bar=False,
            enable_model_summary=False,
        )
        self.device = "gpu" if use_gpu else "cpu"
        pp = self.model.post_processor
        self.model_threshold = float(getattr(pp, "normalized_image_threshold", 0.5))

    def infer(self, image_path_in: str) -> dict:
        import cv2
        import numpy as np

        t0 = time.time()
        win_style = len(image_path_in.strip()) > 2 and image_path_in.strip()[1] == ":"
        path = to_linux(image_path_in)
        if not os.path.isfile(path):
            return {"ok": False, "error": f"파일이 없음: {image_path_in}"}

        preds = self.engine.predict(model=self.model, data_path=path)
        b = preds[0]
        score = float(b.pred_score.flatten()[0])
        thr = self.threshold if self.threshold is not None else self.model_threshold
        if self.threshold is not None:
            is_ng = score >= thr
        else:
            is_ng = bool(b.pred_label.flatten()[0])

        # 히트맵: 원본 위에 색칠한 이미지 저장 (파랑=정상, 빨강=이상)
        img = cv2.imdecode(np.fromfile(path, dtype=np.uint8), cv2.IMREAD_COLOR)
        amap = b.anomaly_map.detach().cpu().numpy().reshape(b.anomaly_map.shape[-2:])
        amap = cv2.resize(amap, (img.shape[1], img.shape[0]))
        amap = np.clip(amap, 0.0, 1.0)
        color = cv2.applyColorMap((amap * 255).astype("uint8"), cv2.COLORMAP_JET)
        overlay = cv2.addWeighted(img, 0.5, color, 0.5, 0)
        # 파일 이름이 겹치지 않게 시각(ms)을 붙인다 (폴더만 다르고 이름이 같은 사진 대비)
        out_path = self.out_dir / f"{Path(path).stem}_{int(time.time() * 1000)}_heatmap.png"
        ok, buf = cv2.imencode(".png", overlay)
        if ok:
            buf.tofile(str(out_path))

        heat = str(out_path)
        if IS_WIN:
            win_style = True  # 윈도우에서는 항상 윈도우 경로로 돌려준다
        return {
            "ok": True,
            "result": "NG" if is_ng else "OK",
            "score": round(score, 4),
            "threshold": round(thr, 4),
            "image_path": image_path_in,
            "heatmap_path": to_windows(heat) if win_style else heat,
            "elapsed_ms": int((time.time() - t0) * 1000),
        }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("image", nargs="?", help="검사할 사진 경로 (--serve 이면 생략)")
    ap.add_argument("--serve", action="store_true", help="켜 두고 stdin 으로 경로를 받음")
    ap.add_argument("--ckpt", default=DEFAULT_CKPT)
    ap.add_argument("--out-dir", default=DEFAULT_OUT)
    ap.add_argument("--threshold", type=float, default=None,
                    help="점수 기준값(0~1). 생략하면 모델의 기준값(0.5 안팎) 사용")
    args = ap.parse_args()

    # 한글이 깨지지 않게 입출력을 UTF-8 로 고정 (윈도우 콘솔 기본값은 cp949)
    try:
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stdin.reconfigure(encoding="utf-8")
    except Exception:
        pass

    # JSON 은 진짜 stdout 으로만 내보내고, 라이브러리가 찍는 글은 stderr 로 돌린다
    real_out = sys.stdout
    sys.stdout = sys.stderr

    def emit(obj):
        real_out.write(json.dumps(obj, ensure_ascii=False) + "\n")
        real_out.flush()

    t0 = time.time()
    try:
        eng = Engine2(to_linux(args.ckpt), to_linux(args.out_dir), args.threshold)
    except Exception as e:  # 모델 로드 실패
        emit({"status": "error", "error": f"모델 로드 실패: {e}"})
        sys.exit(1)
    load_ms = int((time.time() - t0) * 1000)

    if not args.serve:
        if not args.image:
            emit({"ok": False, "error": "사진 경로가 필요함"})
            sys.exit(2)
        try:
            emit(eng.infer(args.image))
        except Exception as e:
            emit({"ok": False, "error": str(e)})
            sys.exit(1)
        return

    emit({"status": "ready", "load_ms": load_ms, "device": eng.device,
          "model_threshold": round(eng.model_threshold, 4)})
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        if line.lower() in ("quit", "exit"):
            emit({"status": "bye"})
            break
        try:
            emit(eng.infer(line))
        except Exception as e:  # 한 장 실패해도 엔진은 계속 켜 둔다
            emit({"ok": False, "error": str(e), "image_path": line})


if __name__ == "__main__":
    main()
