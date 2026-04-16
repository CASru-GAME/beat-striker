/**
 * CurveEditor Class
 * Canvas based value curve editor with spline interpolation.
 */
class CurveEditor {
  constructor(canvas, options = {}) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.points = [{ x: 0, y: options.defaultY ?? 0.5 }, { x: 1, y: options.defaultY ?? 0.5 }];
    this.minY = options.minY ?? 0;
    this.maxY = options.maxY ?? 3;
    this.defaultY = options.defaultY ?? 1;
    this.color = options.color ?? '#22d3ee';
    this.glowColor = options.glowColor ?? 'rgba(34, 211, 238, 0.3)';
    this.labelFormat = options.labelFormat ?? ((v) => v.toFixed(2));
    this.interpolation = options.interpolation ?? 'smooth';
    this.onHover = options.onHover ?? null;
    this.onSelect = options.onSelect ?? null;
    this.logX = options.logX ?? false;
    this.logY = options.logY ?? false;
    this.xMin = options.xMin ?? 0;
    this.xMax = options.xMax ?? 1;

    this.dragging = null;
    this.hovered = null;
    this.selectedIndex = null;
    this.pointRadius = 7;

    this._resizeObserver = new ResizeObserver(() => this._resize());
    this._resizeObserver.observe(canvas.parentElement);
    this._resize();

    canvas.addEventListener('mousedown', (e) => this._onMouseDown(e));
    canvas.addEventListener('mousemove', (e) => this._onMouseMove(e));
    canvas.addEventListener('mouseup', () => this._onMouseUp());
    canvas.addEventListener('mouseleave', () => this._onMouseLeave());
    canvas.addEventListener('dblclick', (e) => this._onDblClick(e));

    canvas.addEventListener('touchstart', (e) => this._onTouchStart(e), { passive: false });
    canvas.addEventListener('touchmove', (e) => this._onTouchMove(e), { passive: false });
    canvas.addEventListener('touchend', () => this._onMouseUp());

    this.draw();
  }

  _resize() {
    const rect = this.canvas.parentElement.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;
    this.canvas.width = rect.width * dpr;
    this.canvas.height = rect.height * dpr;
    this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    this.w = rect.width;
    this.h = rect.height;
    this.draw();
  }

  reset() {
    const defNorm = this.actualToNormY(this.defaultY);
    this.points = [{ x: 0, y: defNorm }, { x: 1, y: defNorm }];
    this.draw();
  }

  normToActualX(nx) {
    if (!this.logX) return nx;
    return this.xMin * Math.pow(this.xMax / this.xMin, nx);
  }

  actualToNormX(val) {
    if (!this.logX) return val;
    return Math.log(val / this.xMin) / Math.log(this.xMax / this.xMin);
  }

  normToActualY(ny) {
    if (!this.logY) return ny * (this.maxY - this.minY) + this.minY;
    return this.minY * Math.pow(this.maxY / this.minY, ny);
  }

  actualToNormY(val) {
    if (!this.logY) return (val - this.minY) / (this.maxY - this.minY);
    return Math.log(val / this.minY) / Math.log(this.maxY / this.minY);
  }

  _toCanvasX(nx) { return nx * this.w; }
  _toCanvasY(ny) { return (1 - ny) * this.h; }
  _fromCanvasX(cx) { return Math.max(0, Math.min(1, cx / this.w)); }
  _fromCanvasY(cy) { return Math.max(0, Math.min(1, 1 - cy / this.h)); }

  _getMousePos(e) {
    const rect = this.canvas.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top };
  }

  _findPoint(pos) {
    for (let i = 0; i < this.points.length; i++) {
      const px = this._toCanvasX(this.points[i].x);
      const py = this._toCanvasY(this.points[i].y);
      const dist = Math.sqrt((pos.x - px) ** 2 + (pos.y - py) ** 2);
      if (dist < this.pointRadius + 6) return i;
    }
    return -1;
  }

  _sortPoints() { this.points.sort((a, b) => a.x - b.x); }

  _onMouseDown(e) {
    const pos = this._getMousePos(e);
    const idx = this._findPoint(pos);
    if (idx >= 0) {
      this.dragging = idx;
      this.selectedIndex = idx;
    } else {
      const nx = this._fromCanvasX(pos.x);
      const ny = this._fromCanvasY(pos.y);
      this.points.push({ x: nx, y: ny });
      this._sortPoints();
      const newIdx = this.points.findIndex(p => p.x === nx && p.y === ny);
      this.dragging = newIdx;
      this.selectedIndex = newIdx;
    }
    this._notifySelection();
    this.draw();
  }

  _onMouseMove(e) {
    const pos = this._getMousePos(e);
    if (this.dragging !== null) {
      let nx = this._fromCanvasX(pos.x);
      const ny = this._fromCanvasY(pos.y);
      if (this.dragging === 0) nx = 0;
      else if (this.dragging === this.points.length - 1) nx = 1;
      this.points[this.dragging].x = nx;
      this.points[this.dragging].y = ny;
      this._sortPoints();
      const newIdx = this.points.findIndex(p => p.x === nx && p.y === ny);
      this.dragging = newIdx;
      this.selectedIndex = newIdx;
      this._notifySelection();
      this.draw();
    } else {
      const idx = this._findPoint(pos);
      const newHovered = idx >= 0 ? idx : null;
      if (newHovered !== this.hovered) {
        this.hovered = newHovered;
        this.canvas.style.cursor = this.hovered !== null ? 'grab' : 'crosshair';
        this.draw();
      }
    }
    if (this.onHover) {
      const nx = this._fromCanvasX(pos.x);
      const val = this.getValueAtNorm(nx);
      this.onHover(val, this.normToActualX(nx));
    }
  }

  _onMouseUp() {
    this.dragging = null;
    this.canvas.style.cursor = this.hovered !== null ? 'grab' : 'crosshair';
  }

  _onMouseLeave() {
    this.dragging = null;
    this.hovered = null;
    this.canvas.style.cursor = 'crosshair';
    this.draw();
  }

  _onDblClick(e) {
    const pos = this._getMousePos(e);
    const idx = this._findPoint(pos);
    if (idx > 0 && idx < this.points.length - 1) {
      this.points.splice(idx, 1);
      this.hovered = null;
      this.selectedIndex = null;
      this._notifySelection();
      this.draw();
    }
  }

  _onTouchStart(e) {
    e.preventDefault();
    const touch = e.touches[0];
    const rect = this.canvas.getBoundingClientRect();
    const pos = { x: touch.clientX - rect.left, y: touch.clientY - rect.top };
    const idx = this._findPoint(pos);
    if (idx >= 0) {
      this.dragging = idx;
      this.selectedIndex = idx;
    } else {
      const nx = this._fromCanvasX(pos.x);
      const ny = this._fromCanvasY(pos.y);
      this.points.push({ x: nx, y: ny });
      this._sortPoints();
      const newIdx = this.points.findIndex(p => p.x === nx && p.y === ny);
      this.dragging = newIdx;
      this.selectedIndex = newIdx;
    }
    this._notifySelection();
    this.draw();
  }

  _onTouchMove(e) {
    e.preventDefault();
    if (this.dragging === null) return;
    const touch = e.touches[0];
    const rect = this.canvas.getBoundingClientRect();
    const pos = { x: touch.clientX - rect.left, y: touch.clientY - rect.top };
    let nx = this._fromCanvasX(pos.x);
    const ny = this._fromCanvasY(pos.y);
    if (this.dragging === 0) nx = 0;
    else if (this.dragging === this.points.length - 1) nx = 1;
    this.points[this.dragging].x = nx;
    this.points[this.dragging].y = ny;
    this._sortPoints();
    this.dragging = this.points.findIndex(p => p.x === nx && p.y === ny);
    this.selectedIndex = this.dragging;
    this._notifySelection();
    this.draw();
  }

  _notifySelection() {
    if (this.onSelect) {
      if (this.selectedIndex !== null && this.selectedIndex < this.points.length) {
        const p = this.points[this.selectedIndex];
        const val = this.normToActualY(p.y);
        const actualX = this.normToActualX(p.x);
        this.onSelect({ index: this.selectedIndex, x: p.x, actualX, value: val });
      } else {
        this.onSelect(null);
      }
    }
  }

  setSelectedValue(val) {
    if (this.selectedIndex === null || this.selectedIndex >= this.points.length) return;
    const ny = Math.max(0, Math.min(1, this.actualToNormY(val)));
    this.points[this.selectedIndex].y = ny;
    this.draw();
  }

  setSelectedX(nx) {
    if (this.selectedIndex === null || this.selectedIndex >= this.points.length) return;
    if (this.selectedIndex === 0 || this.selectedIndex === this.points.length - 1) return;
    this.points[this.selectedIndex].x = Math.max(0, Math.min(1, nx));
    this._sortPoints();
    this.selectedIndex = this.points.findIndex(p => p.x === Math.max(0, Math.min(1, nx)));
    this.draw();
  }

  shiftAll(delta) {
    const normDelta = this.logY
      ? delta / (this.maxY - this.minY)
      : delta / (this.maxY - this.minY);
    for (const p of this.points) { p.y = Math.max(0, Math.min(1, p.y + normDelta)); }
    this.draw();
    if (this.selectedIndex !== null) this._notifySelection();
  }

  setAllToValue(val) {
    const ny = Math.max(0, Math.min(1, this.actualToNormY(val)));
    for (const p of this.points) { p.y = ny; }
    this.draw();
    if (this.selectedIndex !== null) this._notifySelection();
  }

  getAverageValue() {
    if (this.points.length === 0) return this.defaultY;
    const avgNy = this.points.reduce((sum, p) => sum + p.y, 0) / this.points.length;
    return this.normToActualY(avgNy);
  }

  getValueAtNorm(nx) {
    if (this.points.length === 0) return this.defaultY;
    if (nx <= this.points[0].x) return this.normToActualY(this.points[0].y);
    if (nx >= this.points[this.points.length - 1].x) {
      return this.normToActualY(this.points[this.points.length - 1].y);
    }
    let i = 0;
    while (i < this.points.length - 1 && this.points[i + 1].x < nx) i++;
    const p0 = this.points[Math.max(0, i - 1)];
    const p1 = this.points[i];
    const p2 = this.points[Math.min(this.points.length - 1, i + 1)];
    const p3 = this.points[Math.min(this.points.length - 1, i + 2)];
    const t = (nx - p1.x) / (p2.x - p1.x || 1);
    let ny;
    if (this.interpolation === 'smooth') {
      ny = this._catmullRom(p0.y, p1.y, p2.y, p3.y, t);
      ny = Math.max(0, Math.min(1, ny));
    } else {
      ny = p1.y + (p2.y - p1.y) * t;
    }
    return this.normToActualY(ny);
  }

  _catmullRom(y0, y1, y2, y3, t) {
    const t2 = t * t, t3 = t2 * t;
    return 0.5 * ((2*y1) + (-y0+y2)*t + (2*y0-5*y1+4*y2-y3)*t2 + (-y0+3*y1-3*y2+y3)*t3);
  }

  getValueAtTime(timeSec, duration) {
    const nx = duration > 0 ? timeSec / duration : 0;
    return this.getValueAtNorm(nx);
  }

  getValueAtFreq(freq) {
    const nx = this.actualToNormX(Math.max(this.xMin, Math.min(this.xMax, freq)));
    return this.getValueAtNorm(nx);
  }

  scheduleOnParam(param, duration, startTime = 0, steps = 200) {
    for (let i = 0; i <= steps; i++) {
      const t = (i / steps) * duration;
      const val = this.getValueAtTime(t, duration);
      if (i === 0) param.setValueAtTime(val, startTime);
      else param.linearRampToValueAtTime(val, startTime + t);
    }
  }

  /**
   * Schedule this curve on a playbackRate-like param, compensating
   * for the fact that changing rate changes how fast source time advances.
   * Uses exponentialRamp for natural pitch transitions.
   * Returns the total wall-clock duration.
   */
  scheduleOnParamAsRate(param, sourceDuration, startTime = 0, steps = 200) {
    const dt = sourceDuration / steps;
    let wallClock = 0;

    for (let i = 0; i <= steps; i++) {
      const sourceTime = (i / steps) * sourceDuration;
      // Clamp to positive non-zero for exponentialRamp
      const rate = Math.max(0.01, this.getValueAtTime(sourceTime, sourceDuration));

      if (i === 0) {
        param.setValueAtTime(rate, startTime);
      } else {
        const prevRate = Math.max(0.01, this.getValueAtTime(((i - 1) / steps) * sourceDuration, sourceDuration));
        const avgRate = (prevRate + rate) / 2;
        wallClock += dt / avgRate;
        // exponentialRamp is more natural for multiplicative params like playbackRate
        param.exponentialRampToValueAtTime(rate, startTime + wallClock);
      }
    }

    return wallClock;
  }

  isFlat() {
    if (this.points.length <= 2) {
      const defNorm = this.actualToNormY(this.defaultY);
      return this.points.every(p => Math.abs(p.y - defNorm) < 0.005);
    }
    return false;
  }

  draw() {
    const ctx = this.ctx;
    const w = this.w;
    const h = this.h;
    ctx.clearRect(0, 0, w, h);

    // Grid
    ctx.strokeStyle = 'rgba(255,255,255,0.04)';
    ctx.lineWidth = 1;

    if (this.logX) {
      const freqs = [50, 100, 200, 500, 1000, 2000, 5000, 10000];
      ctx.fillStyle = 'rgba(255,255,255,0.12)';
      ctx.font = '9px Inter, sans-serif';
      ctx.textAlign = 'center';
      for (const f of freqs) {
        const nx = this.actualToNormX(f);
        const x = this._toCanvasX(nx);
        ctx.strokeStyle = 'rgba(255,255,255,0.06)';
        ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, h); ctx.stroke();
        const label = f >= 1000 ? (f/1000) + 'k' : f + '';
        ctx.fillText(label, x, h - 4);
      }
    } else {
      for (let i = 1; i < 10; i++) {
        const x = (i / 10) * w;
        ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, h); ctx.stroke();
      }
    }

    ctx.strokeStyle = 'rgba(255,255,255,0.04)';
    for (let i = 1; i < 5; i++) {
      const y = (i / 5) * h;
      ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(w, y); ctx.stroke();
    }

    const defNorm = this.actualToNormY(this.defaultY);
    const defY = this._toCanvasY(defNorm);
    ctx.strokeStyle = 'rgba(255,255,255,0.08)';
    ctx.setLineDash([4, 4]);
    ctx.beginPath(); ctx.moveTo(0, defY); ctx.lineTo(w, defY); ctx.stroke();
    ctx.setLineDash([]);

    ctx.fillStyle = 'rgba(255,255,255,0.15)';
    ctx.font = '10px Inter, sans-serif';
    ctx.textAlign = 'left';
    ctx.fillText(this.labelFormat(this.maxY), 4, 12);
    ctx.fillText(this.labelFormat(this.minY), 4, h - 4);
    ctx.fillText(this.labelFormat(this.defaultY), 4, defY - 4);

    if (this.points.length < 2) return;

    ctx.strokeStyle = this.color;
    ctx.lineWidth = 2;
    ctx.shadowColor = this.glowColor;
    ctx.shadowBlur = 8;
    ctx.beginPath();

    const resolution = Math.max(200, w);
    for (let i = 0; i <= resolution; i++) {
      const nx = i / resolution;
      const val = this.getValueAtNorm(nx);
      const ny = this.actualToNormY(val);
      const cx = this._toCanvasX(nx);
      const cy = this._toCanvasY(ny);
      if (i === 0) ctx.moveTo(cx, cy);
      else ctx.lineTo(cx, cy);
    }
    ctx.stroke();

    ctx.shadowBlur = 0;
    ctx.lineTo(this._toCanvasX(1), h);
    ctx.lineTo(0, h);
    ctx.closePath();
    const grad = ctx.createLinearGradient(0, 0, 0, h);
    grad.addColorStop(0, this.glowColor);
    grad.addColorStop(1, 'transparent');
    ctx.fillStyle = grad;
    ctx.fill();

    for (let i = 0; i < this.points.length; i++) {
      const p = this.points[i];
      const px = this._toCanvasX(p.x);
      const py = this._toCanvasY(p.y);
      const isHovered = i === this.hovered;
      const isDragging = i === this.dragging;
      const isSelected = i === this.selectedIndex;

      if (isSelected && !isDragging) {
        ctx.beginPath();
        ctx.arc(px, py, this.pointRadius + 5, 0, Math.PI * 2);
        ctx.strokeStyle = '#fff';
        ctx.lineWidth = 1.5;
        ctx.setLineDash([3, 3]);
        ctx.stroke();
        ctx.setLineDash([]);
      }

      if (isHovered || isDragging || isSelected) {
        ctx.beginPath();
        ctx.arc(px, py, this.pointRadius + 4, 0, Math.PI * 2);
        ctx.fillStyle = this.glowColor;
        ctx.fill();
      }

      ctx.beginPath();
      ctx.arc(px, py, isDragging ? this.pointRadius + 1 : this.pointRadius, 0, Math.PI * 2);
      ctx.fillStyle = (isHovered || isDragging || isSelected) ? '#fff' : this.color;
      ctx.fill();

      ctx.beginPath();
      ctx.arc(px, py, 3, 0, Math.PI * 2);
      ctx.fillStyle = isDragging ? this.color : '#000';
      ctx.fill();

      if (isHovered || isDragging || isSelected) {
        const val = this.normToActualY(p.y);
        const label = this.labelFormat(val);
        ctx.fillStyle = '#fff';
        ctx.font = '11px Inter, sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText(label, px, py - this.pointRadius - 8);
      }
    }
  }
}
