"""
Local PaddleOCR runner for Cash-for-Work attendance images.

Usage:
  python paddle_ocr_engine.py --health
  python paddle_ocr_engine.py <image_path>
"""

from __future__ import annotations

import json
import os
import platform
import sys
import tempfile
from argparse import ArgumentParser
from pathlib import Path


def _print_json(payload: dict) -> None:
    print(json.dumps(payload, ensure_ascii=False))


def health_check() -> int:
    try:
        os.environ.setdefault("PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK", "True")
        from paddleocr import PaddleOCR  # noqa: F401

        _print_json(
            {
                "success": True,
                "detail": f"PaddleOCR import ok on Python {platform.python_version()}",
            }
        )
        return 0
    except Exception as exc:  # pragma: no cover - shell integration path
        _print_json({"success": False, "error": str(exc)})
        return 1


def _parse_bool(value: str) -> bool:
    return value.strip().lower() in {"1", "true", "yes", "on"}


def _build_ocr(det_model: str, rec_model: str, disable_mkldnn: bool):
    os.environ.setdefault("PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK", "True")
    from paddleocr import PaddleOCR

    return PaddleOCR(
        text_detection_model_name=det_model,
        text_recognition_model_name=rec_model,
        use_doc_orientation_classify=False,
        use_doc_unwarping=False,
        use_textline_orientation=False,
        enable_mkldnn=not disable_mkldnn,
    )


def _prepare_image(image_path: Path) -> Path:
    import cv2

    image = cv2.imread(str(image_path))
    if image is None:
        raise ValueError("Could not read image. Check the file format.")

    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    _, paper_mask = cv2.threshold(gray, 180, 255, cv2.THRESH_BINARY)
    contours, _ = cv2.findContours(
        paper_mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE
    )
    contours = sorted(contours, key=cv2.contourArea, reverse=True)

    crop = image
    for contour in contours:
        x, y, w, h = cv2.boundingRect(contour)
        if w > image.shape[1] * 0.45 and h > image.shape[0] * 0.45:
            pad = 24
            x0 = max(0, x - pad)
            y0 = max(0, y - pad)
            x1 = min(image.shape[1], x + w + pad)
            y1 = min(image.shape[0], y + h + pad)
            crop = image[y0:y1, x0:x1]
            break

    max_side = max(crop.shape[0], crop.shape[1])
    if max_side < 2200:
        scale = min(2.0, 2200.0 / max_side)
        crop = cv2.resize(
            crop,
            None,
            fx=scale,
            fy=scale,
            interpolation=cv2.INTER_CUBIC,
        )

    processed = cv2.fastNlMeansDenoisingColored(crop, None, 3, 3, 7, 21)

    fd, temp_path = tempfile.mkstemp(prefix="cfw-paddle-", suffix=".png")
    os.close(fd)
    cv2.imwrite(temp_path, processed)
    return Path(temp_path)


def _box_to_position(box) -> dict[str, float]:
    if isinstance(box, (list, tuple)) and len(box) == 4 and all(
        isinstance(value, (int, float)) for value in box
    ):
        left, top, right, bottom = box
        return {
            "top": round(float(top), 1),
            "left": round(float(left), 1),
            "right": round(float(right), 1),
            "bottom": round(float(bottom), 1),
        }

    xs = []
    ys = []
    for point in box or []:
        if isinstance(point, (list, tuple)) and len(point) >= 2:
            xs.append(float(point[0]))
            ys.append(float(point[1]))

    if not xs or not ys:
        return {"top": 0.0, "left": 0.0, "right": 0.0, "bottom": 0.0}

    return {
        "top": round(min(ys), 1),
        "left": round(min(xs), 1),
        "right": round(max(xs), 1),
        "bottom": round(max(ys), 1),
    }


def run_ocr(image_path: str, det_model: str, rec_model: str, disable_mkldnn: bool) -> dict:
    try:
        import warnings

        warnings.filterwarnings("ignore", message="urllib3 .* doesn't match")

        source_path = Path(image_path)
        if not source_path.exists():
            return {"success": False, "error": f"File not found: {image_path}"}

        prepared_path = _prepare_image(source_path)
        try:
            ocr = _build_ocr(det_model, rec_model, disable_mkldnn)
            predictions = list(ocr.predict(str(prepared_path)))
        finally:
            prepared_path.unlink(missing_ok=True)

        if not predictions:
            return {"success": True, "total_lines": 0, "lines": [], "raw_text": ""}

        payload = getattr(predictions[0], "json", {})
        if isinstance(payload, str):
            payload = json.loads(payload)

        result = payload.get("res", payload)
        texts = result.get("rec_texts", [])
        scores = result.get("rec_scores", [])
        boxes = result.get("rec_boxes", [])

        lines = []
        for idx, text in enumerate(texts):
            cleaned_text = (text or "").strip()
            if not cleaned_text:
                continue

            confidence = float(scores[idx]) if idx < len(scores) else 0.0
            box = boxes[idx] if idx < len(boxes) else None
            lines.append(
                {
                    "id": idx,
                    "text": cleaned_text,
                    "confidence": round(confidence, 4),
                    "position": _box_to_position(box),
                }
            )

        lines.sort(key=lambda item: (item["position"]["top"], item["position"]["left"]))

        return {
            "success": True,
            "total_lines": len(lines),
            "lines": lines,
            "raw_text": "\n".join(line["text"] for line in lines),
        }
    except Exception as exc:  # pragma: no cover - shell integration path
        return {"success": False, "error": str(exc)}


def _build_parser() -> ArgumentParser:
    parser = ArgumentParser(add_help=False)
    parser.add_argument("image_path", nargs="?")
    parser.add_argument("--health", action="store_true")
    parser.add_argument("--det-model", default="PP-OCRv5_mobile_det")
    parser.add_argument("--rec-model", default="en_PP-OCRv5_mobile_rec")
    parser.add_argument("--disable-mkldnn", default="true")
    return parser


def main(argv: list[str]) -> int:
    args = _build_parser().parse_args(argv[1:])

    if args.health:
        return health_check()

    if not args.image_path:
        _print_json(
            {
                "success": False,
                "error": "No image path provided. Usage: python paddle_ocr_engine.py <image_path>",
            }
        )
        return 1

    _print_json(
        run_ocr(
            args.image_path,
            args.det_model,
            args.rec_model,
            _parse_bool(args.disable_mkldnn),
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
