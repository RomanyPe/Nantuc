export function draw(canvas, b64, w, h) {
    if (!canvas || !b64) return;

    const bin = atob(b64);
    const ctx = canvas.getContext("2d");
    const cw = canvas.width / w;
    const ch = canvas.height / h;

    // фон
    ctx.fillStyle = "#0a0a0a";
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // клетки
    ctx.fillStyle = "#22ff44";
    for (let y = 0; y < h; y++) {
        const row = y * w;
        for (let x = 0; x < w; x++) {
            if (bin.charCodeAt(row + x) !== 0) {
                ctx.fillRect(x * cw + 1, y * ch + 1, cw - 2, ch - 2);
            }
        }
    }
}