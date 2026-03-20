/**
 * blazor-motion.js
 * BlazorMotion animation engine — zero external dependencies.
 * Exported as an ES module so Blazor can import it via JS interop.
 *
 * Architecture:
 *  - ElementState   — per-element bookkeeping (transforms, active rAF handles, gestures)
 *  - Spring solver  — semi-implicit Euler for accurate spring physics
 *  - Tween engine   — rAF-based with full cubic-bezier easing
 *  - WAAPI bridge   — delegates to Web Animations API for non-transform tweens
 *  - Gesture layer  — composited hover / tap / focus / drag states
 *  - FLIP           — snapshot → layout-change → invert → play
 *  - Scroll tracker — IntersectionObserver + scroll event binding
 *  - MotionValue    — reactive numeric values with subscriber callbacks
 */

// ═══════════════════════════════════════════════════════════════════════════════
// Element state
// ═══════════════════════════════════════════════════════════════════════════════

const _elements = new Map();   // elementId → ElementState
const _motionValues = new Map(); // valueId → MotionValueState
const _scrollSubs = new Map();   // subscriptionKey → cleanup fn
const _layoutSnapshots = new Map(); // elementId → DOMRect snapshot

class ElementState {
    constructor(element, dotnetRef) {
        this.element = element;
        this.dotnetRef = dotnetRef;
        this.transforms = {};   // live transform component values
        this.values = {};       // live non-transform values
        this.activeAnims = new Map(); // property → { cancel() }
        this.gestureLayers = {}; // hover/tap/focus/drag → { values, transition }
        this.baseValues = null; // last animate target
        this.baseTransition = null;
        this.cleanupFns = [];
        this.whileInView = null;
        this.whileInViewTransition = null;
        this.viewportOnce = false;
        this.hasEnteredViewport = false;
    }
    cancelProp(property) {
        this.activeAnims.get(property)?.cancel();
        this.activeAnims.delete(property);
    }
    cancelAll() {
        this.activeAnims.forEach(a => a.cancel());
        this.activeAnims.clear();
    }
    dispose() {
        this.cancelAll();
        this.cleanupFns.forEach(fn => fn());
        this.cleanupFns = [];
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Property helpers
// ═══════════════════════════════════════════════════════════════════════════════

const TRANSFORM_PROPS = new Set([
    'x','y','z','rotateX','rotateY','rotateZ','rotate',
    'scaleX','scaleY','scale','skewX','skewY','perspective'
]);

const COLOR_PROPS = new Set([
    'backgroundColor','color','borderColor','outlineColor','fill','stroke',
    'caretColor','columnRuleColor','textDecorationColor'
]);

const isTransform = k => TRANSFORM_PROPS.has(k);
const isColor     = k => COLOR_PROPS.has(k) || k.toLowerCase().includes('color');

function getDefaultValue(key) {
    if (key === 'opacity') return 1;
    if (key === 'scale' || key === 'scaleX' || key === 'scaleY') return 1;
    if (key === 'pathLength') return 1;
    return 0;
}

function getCurrentValue(state, key) {
    if (state.values[key] !== undefined) return state.values[key];
    if (isTransform(key)) return state.transforms[key] ?? getDefaultValue(key);
    const cs = getComputedStyle(state.element);
    if (key === 'opacity') return parseFloat(cs.opacity) || 1;
    if (isColor(key)) return cs[key] || 'rgba(0,0,0,0)';
    return getDefaultValue(key);
}

// ═══════════════════════════════════════════════════════════════════════════════
// Transform composer
// ═══════════════════════════════════════════════════════════════════════════════

function buildTransformString(t) {
    const parts = [];
    if (t.perspective != null) parts.push(`perspective(${t.perspective}px)`);
    const x = t.x ?? 0, y = t.y ?? 0, z = t.z ?? 0;
    if (x !== 0 || y !== 0 || z !== 0)
        parts.push(z !== 0 ? `translate3d(${x}px,${y}px,${z}px)` : `translate(${x}px,${y}px)`);
    if (t.scale != null) parts.push(`scale(${t.scale})`);
    else {
        if (t.scaleX != null) parts.push(`scaleX(${t.scaleX})`);
        if (t.scaleY != null) parts.push(`scaleY(${t.scaleY})`);
    }
    const rz = t.rotateZ ?? t.rotate;
    if (rz != null) parts.push(`rotate(${rz}deg)`);
    if (t.rotateX != null) parts.push(`rotateX(${t.rotateX}deg)`);
    if (t.rotateY != null) parts.push(`rotateY(${t.rotateY}deg)`);
    if (t.skewX != null) parts.push(`skewX(${t.skewX}deg)`);
    if (t.skewY != null) parts.push(`skewY(${t.skewY}deg)`);
    return parts.join(' ');
}

function flushTransforms(state) {
    const str = buildTransformString(state.transforms);
    state.element.style.transform = str || '';
}

function applyProp(state, key, value) {
    if (isTransform(key)) {
        state.transforms[key] = value;
        flushTransforms(state);
    } else if (key === 'opacity') {
        state.element.style.opacity = String(value);
    } else if (key === 'pathLength') {
        state.element.style.strokeDasharray = '1 1';
        state.element.style.strokeDashoffset = String(1 - Math.max(0, Math.min(1, value)));
    } else if (key === 'pathOffset') {
        state.element.style.strokeDashoffset = String(-value);
    } else if (key.startsWith('--')) {
        state.element.style.setProperty(key, typeof value === 'number' ? String(value) : value);
    } else if (isColor(key)) {
        state.element.style[key] = value;
    } else {
        // Generic CSS — numbers assumed px unless the key returns a string
        const cssKey = key.replace(/([A-Z])/g, m => `-${m.toLowerCase()}`);
        state.element.style[cssKey] = typeof value === 'number' ? `${value}px` : value;
    }
    state.values[key] = value;
}

function setInstant(state, values) {
    for (const [k, v] of Object.entries(values))
        if (v !== null && v !== undefined) applyProp(state, k, v);
}

// ═══════════════════════════════════════════════════════════════════════════════
// Color interpolation
// ═══════════════════════════════════════════════════════════════════════════════

function parseColor(c) {
    if (!c || typeof c !== 'string') return null;
    const hex = c.match(/^#([0-9a-f]{3,8})$/i);
    if (hex) {
        const h = hex[1];
        if (h.length <= 4) {
            return [
                parseInt(h[0]+h[0],16), parseInt(h[1]+h[1],16),
                parseInt(h[2]+h[2],16), h.length===4 ? parseInt(h[3]+h[3],16)/255 : 1
            ];
        }
        return [
            parseInt(h.slice(0,2),16), parseInt(h.slice(2,4),16),
            parseInt(h.slice(4,6),16), h.length===8 ? parseInt(h.slice(6,8),16)/255 : 1
        ];
    }
    const rgb = c.match(/rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)(?:\s*,\s*([\d.]+))?\s*\)/);
    if (rgb) return [+rgb[1], +rgb[2], +rgb[3], rgb[4] !== undefined ? +rgb[4] : 1];
    return null;
}

function lerpColor(from, to, t) {
    const f = parseColor(from), tt = parseColor(to);
    if (!f || !tt) return to;
    const r = Math.round(f[0] + (tt[0]-f[0])*t);
    const g = Math.round(f[1] + (tt[1]-f[1])*t);
    const b = Math.round(f[2] + (tt[2]-f[2])*t);
    const a = +(f[3] + (tt[3]-f[3])*t).toFixed(4);
    return `rgba(${r},${g},${b},${a})`;
}

// ═══════════════════════════════════════════════════════════════════════════════
// Easing
// ═══════════════════════════════════════════════════════════════════════════════

function cubicBezier(x1,y1,x2,y2) {
    return function(t) {
        if (t <= 0) return 0;
        if (t >= 1) return 1;
        // Newton-Raphson to find parameter u such that Bx(u) ≈ t, then return By(u)
        let u = t;
        for (let i = 0; i < 10; i++) {
            const bx = 3*u*(1-u)*(1-u)*x1 + 3*u*u*(1-u)*x2 + u*u*u - t;
            const dbx = 3*(1-u)*(1-u)*x1 + 6*u*(1-u)*x2 - 6*u*(1-u)*x1 + 3*u*u;
            if (Math.abs(dbx) < 1e-8) break;
            u -= bx / dbx;
            u = Math.max(0, Math.min(1, u));
        }
        return 3*u*(1-u)*(1-u)*y1 + 3*u*u*(1-u)*y2 + u*u*u;
    };
}

const EASING_FNS = {
    linear:    t => t,
    easeIn:    cubicBezier(0.42, 0, 1, 1),
    easeOut:   cubicBezier(0, 0, 0.58, 1),
    easeInOut: cubicBezier(0.42, 0, 0.58, 1),
    circIn:    t => 1 - Math.sqrt(1 - t*t),
    circOut:   t => Math.sqrt(1 - (t-1)*(t-1)),
    circInOut: t => t < 0.5 ? (1-Math.sqrt(1-4*t*t))/2 : (Math.sqrt(1-(2*t-2)*(2*t-2))+1)/2,
    backIn:    cubicBezier(0.31455, -0.37755, 0.69245, 1.37755),
    backOut:   cubicBezier(0.33915, 0, 0.68085, 1.4),
    backInOut: cubicBezier(0.68987, -0.45, 0.32, 1.45),
    anticipate: t => t < 0.5
        ? cubicBezier(0.31455,-0.37755,0.69245,1.37755)(t*2)/2
        : cubicBezier(0,0,0.58,1)(t*2-1)/2+0.5,
};

function getEasingFn(ease) {
    if (!ease) return EASING_FNS.easeOut;
    if (typeof ease === 'function') return ease;
    if (typeof ease === 'string') return EASING_FNS[ease] ?? EASING_FNS.easeOut;
    if (Array.isArray(ease) && ease.length === 4) return cubicBezier(...ease);
    return EASING_FNS.easeOut;
}

// CSS easing string for WAAPI
function cssEasingStr(ease) {
    if (!ease) return 'ease';
    const map = {linear:'linear',easeIn:'ease-in',easeOut:'ease-out',easeInOut:'ease-in-out'};
    if (typeof ease === 'string') return map[ease] ?? ease;
    if (Array.isArray(ease) && ease.length === 4) return `cubic-bezier(${ease.join(',')})`;
    return 'ease';
}

// ═══════════════════════════════════════════════════════════════════════════════
// Animation drivers
// ═══════════════════════════════════════════════════════════════════════════════

const lerp = (a, b, t) => a + (b - a) * t;

function animateProp(state, key, from, to, transition, onDone) {
    state.cancelProp(key);

    const type = transition?.type ?? 'tween';

    if (Array.isArray(to)) {
        _animateKeyframes(state, key, to, transition, onDone);
    } else if (type === 'spring') {
        _animateSpring(state, key, from, to, transition, onDone);
    } else if (type === 'inertia') {
        _animateInertia(state, key, from, transition, onDone);
    } else {
        _animateTween(state, key, from, to, transition, onDone);
    }
}

// ── Tween ─────────────────────────────────────────────────────────────────────

function _animateTween(state, key, from, to, transition, onDone) {
    const duration  = (transition?.duration ?? 0.3) * 1000;
    const delay     = (transition?.delay    ?? 0)   * 1000;
    const easeFn    = getEasingFn(transition?.ease);
    const repeat    = transition?.repeat    ?? 0;
    const repeatType= transition?.repeatType ?? 'loop';

    const isInfinite = repeat === 'Infinity' || repeat === Infinity;
    let iteration   = 0;
    let startTime   = null;
    let rafId;
    let cancelled   = false;
    let curFrom = from, curTo = to;

    function step(ts) {
        if (cancelled) return;
        if (startTime === null) startTime = ts + delay;
        if (ts < startTime) { rafId = requestAnimationFrame(step); return; }

        const t = Math.min((ts - startTime) / duration, 1);
        const p = easeFn(t);
        const cur = isColor(key) ? lerpColor(curFrom, curTo, p) : lerp(+curFrom || 0, +curTo || 0, p);
        applyProp(state, key, cur);

        if (t < 1) { rafId = requestAnimationFrame(step); return; }

        // iteration done
        if (isInfinite || iteration < repeat) {
            iteration++;
            startTime = ts + (transition?.repeatDelay ?? 0) * 1000;
            if (repeatType === 'mirror' || repeatType === 'reverse') [curFrom, curTo] = [curTo, curFrom];
            rafId = requestAnimationFrame(step);
        } else {
            applyProp(state, key, to);
            state.activeAnims.delete(key);
            onDone?.(key);
        }
    }
    rafId = requestAnimationFrame(step);
    state.activeAnims.set(key, { cancel() { cancelled = true; cancelAnimationFrame(rafId); } });
}

// ── Spring ────────────────────────────────────────────────────────────────────

function _animateSpring(state, key, from, to, transition, onDone) {
    const k  = transition?.stiffness ?? 100;
    const d  = transition?.damping   ?? 10;
    const m  = transition?.mass      ?? 1;
    const restSpeed = transition?.restSpeed ?? 0.01;
    const restDelta = transition?.restDelta ?? 0.01;
    const delay     = (transition?.delay ?? 0) * 1000;

    let pos = +from || 0;
    const target = +to || 0;
    let vel = transition?.velocity ?? 0;
    let lastTs = null;
    let startTs = null;
    let rafId;
    let cancelled = false;

    // Semi-implicit Euler is stable only when d*dt < 2 and k*dt² < 1.
    // For high-damping / high-stiffness configs the raw frame dt can violate
    // these conditions and cause the position to blow up.  Compute an upper
    // bound on the sub-step size that keeps both conditions satisfied, then
    // divide each frame into as many sub-steps as required.
    const maxSubDt = Math.max(0.001, Math.min(
        d > 0 ? (1.8 / d) : 1,
        k > 0 ? (0.9 / Math.sqrt(k)) : 1
    ));

    function step(ts) {
        if (cancelled) return;
        if (startTs === null) startTs = ts;
        if (ts - startTs < delay) { rafId = requestAnimationFrame(step); return; }
        if (lastTs === null) lastTs = ts;

        const dt = Math.min((ts - lastTs) / 1000, 0.064);
        lastTs = ts;

        const subSteps = Math.ceil(dt / maxSubDt);
        const subDt    = dt / subSteps;
        for (let i = 0; i < subSteps; i++) {
            const springF = -k * (pos - target);
            const dampF   = -d * vel;
            vel += (springF + dampF) / m * subDt;
            pos += vel * subDt;
        }

        applyProp(state, key, pos);

        if (Math.abs(vel) < restSpeed && Math.abs(pos - target) < restDelta) {
            applyProp(state, key, target);
            state.activeAnims.delete(key);
            onDone?.(key);
            return;
        }
        rafId = requestAnimationFrame(step);
    }
    rafId = requestAnimationFrame(step);
    state.activeAnims.set(key, { cancel() { cancelled = true; cancelAnimationFrame(rafId); } });
}

// ── Inertia ───────────────────────────────────────────────────────────────────

function _animateInertia(state, key, from, transition, onDone) {
    const power        = transition?.power        ?? 0.8;
    const timeConstant = (transition?.timeConstant ?? 700) / 1000;
    const restDelta    = transition?.inertiaRestDelta ?? 0.5;
    const boundsMin    = transition?.inertiaMin;
    const boundsMax    = transition?.inertiaMax;
    const delay        = (transition?.delay ?? 0) * 1000;
    let velocity       = transition?.inertiaVelocity ?? 0;

    const start = +from || 0;
    let projected = start + power * velocity;
    if (boundsMax !== undefined) projected = Math.min(projected, boundsMax);
    if (boundsMin !== undefined) projected = Math.max(projected, boundsMin);
    const delta = projected - start;

    let elapsed = 0;
    let lastTs = null;
    let startTs = null;
    let rafId;
    let cancelled = false;

    function step(ts) {
        if (cancelled) return;
        if (startTs === null) startTs = ts;
        if (ts - startTs < delay) { rafId = requestAnimationFrame(step); return; }
        if (lastTs === null) lastTs = ts;

        elapsed += Math.min((ts - lastTs) / 1000, 0.064);
        lastTs = ts;

        const pos = start + delta * (1 - Math.exp(-elapsed / timeConstant));
        applyProp(state, key, pos);

        if (Math.abs(projected - pos) < restDelta) {
            applyProp(state, key, projected);
            state.activeAnims.delete(key);
            onDone?.(key);
            return;
        }
        rafId = requestAnimationFrame(step);
    }
    rafId = requestAnimationFrame(step);
    state.activeAnims.set(key, { cancel() { cancelled = true; cancelAnimationFrame(rafId); } });
}

// ── Keyframes ─────────────────────────────────────────────────────────────────

function _animateKeyframes(state, key, frames, transition, onDone) {
    const n        = frames.length;
    const duration = (transition?.duration ?? 1) * 1000;
    const delay    = (transition?.delay    ?? 0) * 1000;
    const times    = transition?.times ?? frames.map((_,i) => i / (n - 1));
    const eases    = Array.isArray(transition?.ease) && typeof transition.ease[0] !== 'number'
                     ? transition.ease.map(getEasingFn)
                     : new Array(n).fill(getEasingFn(transition?.ease));
    const repeat     = transition?.repeat   ?? 0;
    const repeatType = transition?.repeatType ?? 'loop';
    const isInfinite = repeat === 'Infinity' || repeat === Infinity;

    let iteration = 0;
    let startTime = null;
    let rafId;
    let cancelled = false;
    let curFrames = [...frames];

    function step(ts) {
        if (cancelled) return;
        if (startTime === null) startTime = ts + delay;
        if (ts < startTime) { rafId = requestAnimationFrame(step); return; }

        const t = Math.min((ts - startTime) / duration, 1);

        // find segment
        let seg = n - 2;
        for (let i = 0; i < n - 1; i++) {
            if (t <= times[i + 1]) { seg = i; break; }
        }
        const segLen = times[seg+1] - times[seg];
        const segT   = segLen > 0 ? (t - times[seg]) / segLen : 1;
        const easedT = (eases[seg] ?? EASING_FNS.easeOut)(Math.min(segT, 1));

        const fv = curFrames[seg], tv = curFrames[seg+1];
        const cur = isColor(key) ? lerpColor(fv, tv, easedT) : lerp(+fv||0, +tv||0, easedT);
        applyProp(state, key, cur);

        if (t < 1) { rafId = requestAnimationFrame(step); return; }

        if (isInfinite || iteration < repeat) {
            iteration++;
            startTime = ts + (transition?.repeatDelay ?? 0) * 1000;
            if (repeatType === 'mirror' || repeatType === 'reverse') curFrames.reverse();
            rafId = requestAnimationFrame(step);
        } else {
            applyProp(state, key, frames[frames.length - 1]);
            state.activeAnims.delete(key);
            onDone?.(key);
        }
    }
    rafId = requestAnimationFrame(step);
    state.activeAnims.set(key, { cancel() { cancelled = true; cancelAnimationFrame(rafId); } });
}

// ═══════════════════════════════════════════════════════════════════════════════
// Core animateTo
// ═══════════════════════════════════════════════════════════════════════════════

function _runAnimateTo(state, values, transition, resolve) {
    const entries = Object.entries(values).filter(([,v]) => v !== null && v !== undefined);
    if (entries.length === 0) { resolve?.(); return; }

    let done = 0;
    const total = entries.length;

    for (const [key, value] of entries) {
        const from = getCurrentValue(state, key);
        const perKey = transition?.properties?.[key] ?? transition;

        animateProp(state, key, from, value, perKey, () => {
            if (++done >= total) {
                resolve?.();
                state.dotnetRef?.invokeMethodAsync('OnAnimationComplete');
            }
        });
    }
    state.dotnetRef?.invokeMethodAsync('OnAnimationStart');
}

// ═══════════════════════════════════════════════════════════════════════════════
// Gesture layer
// ═══════════════════════════════════════════════════════════════════════════════

const GESTURE_PRIORITY = ['drag','focus','tap','hover','inview'];

function _revertGesture(elementId, layer) {
    const state = _elements.get(elementId);
    if (!state) return;
    delete state.gestureLayers[layer];
    // find highest priority remaining gesture
    for (const p of GESTURE_PRIORITY) {
        const g = state.gestureLayers[p];
        if (g) { _runAnimateTo(state, g.values, g.transition, null); return; }
    }
    // fallback to base
    if (state.baseValues) _runAnimateTo(state, state.baseValues, state.baseTransition, null);
}

function _gestureAnimateTo(elementId, values, transition, layer) {
    const state = _elements.get(elementId);
    if (!state) return;
    state.gestureLayers[layer] = { values, transition };
    _runAnimateTo(state, values, transition, null);
}

// ═══════════════════════════════════════════════════════════════════════════════
// Gestures attachment
// ═══════════════════════════════════════════════════════════════════════════════

function _attachGestures(elementId, opts) {
    const state = _elements.get(elementId);
    if (!state) return;
    const el = state.element;

    // ── Hover ──────────────────────────────────────────────────────────────────
    if (opts.hover || opts.whileHover) {
        const enter = () => {
            if (opts.whileHover) _gestureAnimateTo(elementId, opts.whileHover, opts.hoverTransition, 'hover');
            state.dotnetRef?.invokeMethodAsync('OnHoverStart');
        };
        const leave = () => {
            if (opts.whileHover) _revertGesture(elementId, 'hover');
            state.dotnetRef?.invokeMethodAsync('OnHoverEnd');
        };
        el.addEventListener('pointerenter', enter);
        el.addEventListener('pointerleave', leave);
        state.cleanupFns.push(() => {
            el.removeEventListener('pointerenter', enter);
            el.removeEventListener('pointerleave', leave);
        });
    }

    // ── Tap ────────────────────────────────────────────────────────────────────
    if (opts.tap || opts.whileTap) {
        let pressing = false;
        const down = (e) => {
            pressing = true;
            if (opts.whileTap) _gestureAnimateTo(elementId, opts.whileTap, opts.tapTransition, 'tap');
            state.dotnetRef?.invokeMethodAsync('OnTapStart');
        };
        const up = (e) => {
            if (!pressing) return;
            pressing = false;
            if (opts.whileTap) _revertGesture(elementId, 'tap');
            if (el.contains(e.target) || el === e.target)
                state.dotnetRef?.invokeMethodAsync('OnTap');
        };
        const cancel = () => {
            if (!pressing) return;
            pressing = false;
            if (opts.whileTap) _revertGesture(elementId, 'tap');
            state.dotnetRef?.invokeMethodAsync('OnTapCancel');
        };
        el.addEventListener('pointerdown', down);
        window.addEventListener('pointerup', up);
        window.addEventListener('pointercancel', cancel);
        state.cleanupFns.push(() => {
            el.removeEventListener('pointerdown', down);
            window.removeEventListener('pointerup', up);
            window.removeEventListener('pointercancel', cancel);
        });
    }

    // ── Focus ──────────────────────────────────────────────────────────────────
    if (opts.focus || opts.whileFocus) {
        const focusin  = () => {
            if (opts.whileFocus) _gestureAnimateTo(elementId, opts.whileFocus, opts.focusTransition, 'focus');
            state.dotnetRef?.invokeMethodAsync('OnFocusStart');
        };
        const focusout = () => {
            if (opts.whileFocus) _revertGesture(elementId, 'focus');
            state.dotnetRef?.invokeMethodAsync('OnFocusEnd');
        };
        el.addEventListener('focusin',  focusin);
        el.addEventListener('focusout', focusout);
        state.cleanupFns.push(() => {
            el.removeEventListener('focusin',  focusin);
            el.removeEventListener('focusout', focusout);
        });
    }

    // ── Drag ───────────────────────────────────────────────────────────────────
    if (opts.drag) _attachDrag(elementId, opts);
}

// ═══════════════════════════════════════════════════════════════════════════════
// Drag
// ═══════════════════════════════════════════════════════════════════════════════

function _attachDrag(elementId, opts) {
    const state = _elements.get(elementId);
    if (!state) return;
    const el = state.element;

    const axis        = opts.dragAxis ?? null;       // 'x'|'y'|null
    const constraints = opts.dragConstraints ?? null;
    const elastic     = opts.dragElastic ?? 0.35;
    const momentum    = opts.dragMomentum !== false;

    let dragging = false;
    let startPX, startPY, startElX, startElY;
    let lastPX, lastPY, lastT;
    let velX = 0, velY = 0;

    function applyElastic(overflow) {
        if (!elastic || elastic === 0) return 0;
        const e = elastic === true ? 0.35 : elastic;
        return overflow * e;
    }

    function clamp(state) {
        let x = state.transforms.x ?? 0;
        let y = state.transforms.y ?? 0;
        let changed = false;
        if (constraints) {
            const snapT = opts.dragSnapTransition ?? { type:'spring', stiffness:400, damping:35 };
            if (axis !== 'y') {
                if (constraints.left  !== undefined && x < constraints.left)  { _animateSpring(state,'x',x,constraints.left,snapT,null);  changed=true; }
                if (constraints.right !== undefined && x > constraints.right) { _animateSpring(state,'x',x,constraints.right,snapT,null); changed=true; }
            }
            if (axis !== 'x') {
                if (constraints.top    !== undefined && y < constraints.top)    { _animateSpring(state,'y',y,constraints.top,snapT,null);    changed=true; }
                if (constraints.bottom !== undefined && y > constraints.bottom) { _animateSpring(state,'y',y,constraints.bottom,snapT,null); changed=true; }
            }
        }
        return changed;
    }

    const onDown = (e) => {
        if (e.button !== 0 && e.pointerType !== 'touch') return;
        dragging = true;
        startPX = e.clientX; startPY = e.clientY;
        startElX = state.transforms.x ?? 0;
        startElY = state.transforms.y ?? 0;
        lastPX = e.clientX; lastPY = e.clientY; lastT = Date.now();
        velX = 0; velY = 0;
        el.setPointerCapture(e.pointerId);
        if (opts.whileDrag) _gestureAnimateTo(elementId, opts.whileDrag, opts.dragTransition, 'drag');
        state.dotnetRef?.invokeMethodAsync('OnDragStart', { x: startElX, y: startElY });
    };

    const onMove = (e) => {
        if (!dragging) return;
        const now = Date.now(), dt = now - lastT;
        if (dt > 0) { velX = (e.clientX - lastPX)/dt*16; velY = (e.clientY - lastPY)/dt*16; }
        lastPX = e.clientX; lastPY = e.clientY; lastT = now;

        let dx = axis === 'y' ? 0 : e.clientX - startPX;
        let dy = axis === 'x' ? 0 : e.clientY - startPY;
        let x = startElX + dx, y = startElY + dy;

        if (constraints) {
            if (constraints.left   !== undefined && x < constraints.left)   x = constraints.left  - applyElastic(constraints.left   - x);
            if (constraints.right  !== undefined && x > constraints.right)  x = constraints.right + applyElastic(x - constraints.right);
            if (constraints.top    !== undefined && y < constraints.top)    y = constraints.top   - applyElastic(constraints.top    - y);
            if (constraints.bottom !== undefined && y > constraints.bottom) y = constraints.bottom+ applyElastic(y - constraints.bottom);
        }

        if (axis !== 'y') applyProp(state, 'x', x);
        if (axis !== 'x') applyProp(state, 'y', y);
        state.dotnetRef?.invokeMethodAsync('OnDrag', { x: state.transforms.x??0, y: state.transforms.y??0 });
    };

    const onUp = (e) => {
        if (!dragging) return;
        dragging = false;
        if (opts.whileDrag) _revertGesture(elementId, 'drag');

        if (momentum) {
            const cx = state.transforms.x ?? 0, cy = state.transforms.y ?? 0;
            if (axis !== 'y' && Math.abs(velX) > 0.5)
                _animateInertia(state, 'x', cx, { type:'inertia', inertiaVelocity: velX*50,
                    inertiaMin: constraints?.left, inertiaMax: constraints?.right, ...opts.dragTransition }, null);
            if (axis !== 'x' && Math.abs(velY) > 0.5)
                _animateInertia(state, 'y', cy, { type:'inertia', inertiaVelocity: velY*50,
                    inertiaMin: constraints?.top,  inertiaMax: constraints?.bottom, ...opts.dragTransition }, null);
        }
        if (constraints && !momentum) clamp(state);

        state.dotnetRef?.invokeMethodAsync('OnDragEnd', { x: state.transforms.x??0, y: state.transforms.y??0, velocityX: velX, velocityY: velY });
    };

    el.style.cursor     = 'grab';
    el.style.userSelect = 'none';
    el.style.touchAction = axis === 'x' ? 'pan-y' : axis === 'y' ? 'pan-x' : 'none';

    el.addEventListener('pointerdown',  onDown);
    el.addEventListener('pointermove',  onMove);
    el.addEventListener('pointerup',    onUp);
    el.addEventListener('pointercancel',onUp);

    state.cleanupFns.push(() => {
        el.removeEventListener('pointerdown',  onDown);
        el.removeEventListener('pointermove',  onMove);
        el.removeEventListener('pointerup',    onUp);
        el.removeEventListener('pointercancel',onUp);
        el.style.cursor  = '';
        el.style.userSelect = '';
        el.style.touchAction = '';
    });
}

// ═══════════════════════════════════════════════════════════════════════════════
// Viewport (whileInView)
// ═══════════════════════════════════════════════════════════════════════════════

let _vpObserver = null;
function _getViewportObserver() {
    if (_vpObserver) return _vpObserver;
    _vpObserver = new IntersectionObserver((entries) => {
        for (const entry of entries) {
            const id = entry.target.dataset.bmid;
            const state = _elements.get(id);
            if (!state) continue;

            if (entry.isIntersecting) {
                if (state.viewportOnce && state.hasEnteredViewport) continue;
                state.hasEnteredViewport = true;
                if (state.whileInView) _gestureAnimateTo(id, state.whileInView, state.whileInViewTransition, 'inview');
                state.dotnetRef?.invokeMethodAsync('OnViewportEnter');
            } else {
                if (!state.viewportOnce) {
                    if (state.whileInView) _revertGesture(id, 'inview');
                }
                state.dotnetRef?.invokeMethodAsync('OnViewportLeave');
            }
        }
    }, { threshold: [0, 0.1, 0.5, 1.0] });
    return _vpObserver;
}

// ═══════════════════════════════════════════════════════════════════════════════
// Layout (FLIP)
// ═══════════════════════════════════════════════════════════════════════════════

function captureLayout(elementId) {
    const state = _elements.get(elementId);
    if (!state) return;
    _layoutSnapshots.set(elementId, { ...state.element.getBoundingClientRect().toJSON() });
}

function playLayoutAnimation(elementId, transition) {
    const state = _elements.get(elementId);
    if (!state) return;
    const snap = _layoutSnapshots.get(elementId);
    if (!snap) return;

    const cur = state.element.getBoundingClientRect();
    const dx = snap.left - cur.left;
    const dy = snap.top  - cur.top;
    const sx = snap.width  / cur.width;
    const sy = snap.height / cur.height;

    if (Math.abs(dx)<0.5 && Math.abs(dy)<0.5 && Math.abs(sx-1)<0.005 && Math.abs(sy-1)<0.005) {
        _layoutSnapshots.delete(elementId); return;
    }

    const el = state.element;
    el.style.transformOrigin = '0 0';
    el.style.transform = `translate(${dx}px,${dy}px) scaleX(${sx}) scaleY(${sy})`;
    // force reflow
    el.getBoundingClientRect();

    const t = transition ?? { type:'spring', stiffness:500, damping:35 };
    const dur = t.type === 'spring' ? 600 : (t.duration ?? 0.5) * 1000;

    const anim = el.animate(
        [{ transform: `translate(${dx}px,${dy}px) scaleX(${sx}) scaleY(${sy})` },
         { transform: 'translate(0px,0px) scaleX(1) scaleY(1)' }],
        { duration: dur, easing: t.type === 'spring' ? 'cubic-bezier(0.14,1,0.34,1)' : cssEasingStr(t.ease), fill: 'forwards' }
    );
    anim.onfinish = () => {
        el.style.transform = buildTransformString(state.transforms) || '';
        el.style.transformOrigin = '';
        _layoutSnapshots.delete(elementId);
    };
}

// ═══════════════════════════════════════════════════════════════════════════════
// Scroll tracking
// ═══════════════════════════════════════════════════════════════════════════════

let _scrollKeySeq = 0;

function observeScroll(containerId, dotnetRef) {
    const el = containerId ? document.getElementById(containerId) : window;
    if (!el) return null;
    const key = `scroll_${++_scrollKeySeq}`;

    const onScroll = () => {
        let sX, sY, sW, sH, cW, cH;
        if (el === window) {
            sX = window.scrollX; sY = window.scrollY;
            sW = document.documentElement.scrollWidth;
            sH = document.documentElement.scrollHeight;
            cW = window.innerWidth; cH = window.innerHeight;
        } else {
            sX = el.scrollLeft; sY = el.scrollTop;
            sW = el.scrollWidth; sH = el.scrollHeight;
            cW = el.clientWidth;  cH = el.clientHeight;
        }
        const pX = sW > cW ? sX / (sW - cW) : 0;
        const pY = sH > cH ? sY / (sH - cH) : 0;
        dotnetRef.invokeMethodAsync('OnScroll', { scrollX:sX, scrollY:sY, progressX:pX, progressY:pY, scrollWidth:sW, scrollHeight:sH, clientWidth:cW, clientHeight:cH });
    };

    el.addEventListener('scroll', onScroll, { passive: true });
    _scrollSubs.set(key, () => el.removeEventListener('scroll', onScroll));
    // fire immediately
    onScroll();
    return key;
}

function unobserveScroll(key) {
    _scrollSubs.get(key)?.();
    _scrollSubs.delete(key);
}

function observeElementScroll(elementId, dotnetRef, offset) {
    const el = document.getElementById(elementId);
    if (!el) return null;
    const key = `elscroll_${++_scrollKeySeq}`;

    const io = new IntersectionObserver((entries) => {
        for (const entry of entries) {
            const ratio = entry.intersectionRatio;
            dotnetRef.invokeMethodAsync('OnElementScroll', { progress: ratio, isIntersecting: entry.isIntersecting });
        }
    }, { threshold: Array.from({length:101}, (_,i) => i/100) });

    io.observe(el);
    _scrollSubs.set(key, () => io.unobserve(el));
    return key;
}

// ═══════════════════════════════════════════════════════════════════════════════
// Motion values
// ═══════════════════════════════════════════════════════════════════════════════

class MotionValueState {
    constructor(id, value) { this.id = id; this.value = value; this.subs = []; }
    notify() { this.subs.forEach(s => s.invokeMethodAsync('OnMotionValueChange', this.value)); }
}

function createMotionValue(id, initial) {
    _motionValues.set(id, new MotionValueState(id, initial));
}

function setMotionValue(id, value) {
    const mv = _motionValues.get(id);
    if (!mv) return;
    mv.value = value;
    mv.notify();
}

function getMotionValue(id) {
    return _motionValues.get(id)?.value ?? 0;
}

function destroyMotionValue(id) {
    _motionValues.delete(id);
}

// ═══════════════════════════════════════════════════════════════════════════════
// Public API (ES module exports)
// ═══════════════════════════════════════════════════════════════════════════════

export function init(elementId, dotnetRef, config) {
    const el = document.getElementById(elementId);
    if (!el) return false;

    el.dataset.bmid = elementId;
    const state = new ElementState(el, dotnetRef);
    _elements.set(elementId, state);

    if (config?.initial) setInstant(state, config.initial);

    if (config?.whileInView || config?.observeViewport) {
        state.whileInView           = config.whileInView ?? null;
        state.whileInViewTransition = config.whileInViewTransition ?? null;
        state.viewportOnce          = config.viewportOnce ?? false;
        _getViewportObserver().observe(el);
        state.cleanupFns.push(() => _getViewportObserver().unobserve(el));
    }

    // Attach gestures if provided at init time
    if (config?.gestures && Object.keys(config.gestures).length > 0) {
        _attachGestures(elementId, config.gestures);
    }

    return true;
}

export function dispose(elementId) {
    const state = _elements.get(elementId);
    if (!state) return;
    if (state.element.dataset.bmid) {
        _getViewportObserver().unobserve(state.element);
    }
    state.dispose();
    _elements.delete(elementId);
}

export function set(elementId, values) {
    const state = _elements.get(elementId);
    if (!state) return;
    setInstant(state, values);
}

export function animateTo(elementId, values, transition) {
    const state = _elements.get(elementId);
    if (!state) return;
    state.baseValues     = values;
    state.baseTransition = transition;
    _runAnimateTo(state, values, transition, null);
}

export function animateToAwait(elementId, values, transition) {
    return new Promise(resolve => {
        const state = _elements.get(elementId);
        if (!state) { resolve(); return; }
        state.baseValues     = values;
        state.baseTransition = transition;
        _runAnimateTo(state, values, transition, resolve);
    });
}

export function animateKeyframes(elementId, property, frames, transition) {
    const state = _elements.get(elementId);
    if (!state) return;
    const from = getCurrentValue(state, property);
    _animateKeyframes(state, property, [from, ...frames], transition, null);
}

export function stop(elementId, properties) {
    const state = _elements.get(elementId);
    if (!state) return;
    if (!properties || properties.length === 0) state.cancelAll();
    else properties.forEach(p => state.cancelProp(p));
}

export function attachGestures(elementId, opts) {
    _attachGestures(elementId, opts);
}

export { captureLayout, playLayoutAnimation };
export { observeScroll, unobserveScroll, observeElementScroll };
export { createMotionValue, setMotionValue, getMotionValue, destroyMotionValue };
