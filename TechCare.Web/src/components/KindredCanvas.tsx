import { useEffect, useRef } from 'react'

export function KindredCanvas() {
  const canvasRef = useRef<HTMLCanvasElement | null>(null)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d', { alpha: true })
    if (!ctx) return

    // Primary: repair sheet; Fallback: high-contrast Higgsfield sheet
    const SHEET_URL =
      'https://images.higgs.ai/?default=1&output=webp&url=https%3A%2F%2Fd8j0ntlcm91z4.cloudfront.net%2Fuser_38xzZboKViGWJOttwIXH07lWA1P%2Fhf_20261030_025250_100df7b8-31fd-4614-aa4a-c1b7d5b6b273.png&w=1600&q=85'

    const img = new Image()
    img.crossOrigin = 'anonymous'

    let W = 0,
      H = 0,
      DPR = 1,
      step = 7,
      rowStep = 14,
      cols = 0,
      rows = 0,
      R = 3.2,
      fA: HTMLCanvasElement,
      fB: HTMLCanvasElement,
      cA: CanvasRenderingContext2D | null,
      cB: CanvasRenderingContext2D | null,
      ready = false

    const tiles: Record<string, HTMLCanvasElement> = {}
    const source: Record<string, number[]> = {
      LA: [0, 0.06, 0.47, 0.4],
      RA: [0.53, 0.06, 0.47, 0.4],
      LB: [0, 0.55, 0.47, 0.4],
      RB: [0.53, 0.55, 0.47, 0.4]
    }

    const clamp = (v: number, a: number, b: number) => Math.max(a, Math.min(b, v))
    const smooth = (a: number, b: number, x: number) => {
      const q = clamp((x - a) / (b - a), 0, 1)
      return q * q * (3 - 2 * q)
    }
    const mixAt = (t: number, on: number, off: number, dur: number) => {
      return smooth(on, on + dur, t) * (1 - smooth(off, off + dur, t))
    }
    const hash = (a: number, b: number) => {
      const x = Math.sin(a * 127.1 + b * 311.7) * 43758.5453
      return x - Math.floor(x)
    }

    function size() {
      if (!canvas || !ctx) return
      const rect = canvas.getBoundingClientRect()
      W = rect.width
      H = rect.height
      DPR = Math.min(2, window.devicePixelRatio || 1)
      canvas.width = Math.round(W * DPR)
      canvas.height = Math.round(H * DPR)
      ctx.setTransform(DPR, 0, 0, DPR, 0, 0)
      step = Math.max(5, W / 205)
      rowStep = step * 2.05
      cols = Math.ceil(W / step)
      rows = Math.ceil(H / rowStep)
      R = step * 0.46
      fA = document.createElement('canvas')
      fB = document.createElement('canvas')
      fA.width = fB.width = cols
      fA.height = fB.height = rows
      cA = fA.getContext('2d', { willReadFrequently: true })
      cB = fB.getContext('2d', { willReadFrequently: true })
    }

    function makeTiles() {
      for (const [key, rect] of Object.entries(source)) {
        const [x, y, w, h] = rect
        const tile = document.createElement('canvas')
        tile.width = 420
        tile.height = Math.round((420 * h * img.height) / (w * img.width))
        const tc = tile.getContext('2d')
        if (tc) {
          tc.imageSmoothingEnabled = true
          tc.imageSmoothingQuality = 'high'
          tc.drawImage(img, x * img.width, y * img.height, w * img.width, h * img.height, 0, 0, tile.width, tile.height)
        }
        tiles[key] = tile
      }
      ready = true
    }

    function drawPair(target: CanvasRenderingContext2D, leftKey: string, rightKey: string, t: number) {
      const sx = cols / W
      const sy = rows / H
      target.save()
      target.setTransform(sx, 0, 0, sy, 0, 0)
      target.fillStyle = '#fff'
      target.fillRect(0, 0, W, H)
      const portrait = H > W
      const hw = W * (portrait ? 0.66 : 0.53)
      const hh = (hw * (0.4 * 1520)) / (0.47 * 2688)
      const cy = H * (portrait ? 0.66 : 0.54)
      const loop = 20
      const a = (Math.PI * 2 * t) / loop
      for (const hand of ['left', 'right']) {
        const left = hand === 'left'
        const ph = left ? 0 : Math.PI
        const reach = (0.035 * Math.sin(a + ph) + 0.012 * Math.sin(2 * a + 1.3 + ph)) * W
        const scale = 1 + 0.07 * Math.sin(a + 0.9 + ph)
        const rot = (left ? 1 : -1) * 0.045 * Math.sin(a + 2.1 + ph)
        const x = left ? reach - W * 0.045 : W - hw + W * 0.045 - reach
        const pivot = left ? 0 : W
        target.save()
        target.translate(pivot, cy)
        target.rotate(rot)
        target.scale(scale, scale)
        target.translate(-pivot, -cy)
        const tile = tiles[left ? leftKey : rightKey]
        if (tile) {
          target.drawImage(tile, x, cy - hh / 2, hw, hh)
        }
        target.restore()
      }
      target.restore()
    }

    function render(time: number) {
      if (!ready || !ctx || !cA || !cB) return
      const t = ((time % 20) + 20) % 20
      drawPair(cA, 'LA', 'RA', t)
      drawPair(cB, 'LB', 'RB', t)
      const A = cA.getImageData(0, 0, cols, rows).data
      const B = cB.getImageData(0, 0, cols, rows).data
      ctx.clearRect(0, 0, W, H)
      ctx.fillStyle = '#0d0d0d'
      const tick = Math.floor(t * 7)
      const portrait = H > W

      // Left (laptop): mL, Right (tool/hand): mR
      const mL = mixAt(t, 6.2, 14.6, 1.6)
      const mR = mixAt(t, 2.4, 10.4, 1.6)

      for (let j = 0; j < rows; j++) {
        const rowJump = hash(j, tick) > 0.965 ? Math.round((hash(tick, j) - 0.5) * 8) : 0
        for (let i = 0; i < cols; i++) {
          const seed = 0.55 * hash(i, j) + 0.45 * hash(j * 3.1, 7.7)
          const m = i < cols / 2 ? mL : mR
          const sample = clamp(i - rowJump, 0, cols - 1)
          const useB = seed < m
          const data = useB ? B : A
          const index = (j * cols + sample) * 4
          const lum = data[index] / 255
          let v = clamp((1 - lum - 0.16) / 0.42, 0, 1)

          // Shimmer
          v *= 0.86 + 0.14 * Math.sin(t * 3.2 + seed * 40)

          // Center fade
          const cx = Math.abs((i + 0.5) / cols - 0.5)
          v *= smooth(0.035, 0.11, cx)

          // Quiet zone for copy block
          const ex = cx / (portrait ? 0.46 : 0.22)
          const ey = ((j + 0.5) * rowStep / H - (portrait ? 0.33 : 0.38)) / 0.10
          v *= smooth(0.85, 1.25, Math.hypot(ex, ey))

          // Front sparks glitch
          if (m > 0.001 && m < 0.999 && Math.abs(seed - m) < 0.12) {
            v *= Math.abs(seed - m) / 0.12
            if (hash(i + tick, j) <= 0.4) v *= 0.3
          }

          // Quantized Levels
          const levels = [0, 0.26, 0.52, 0.78, 1]
          let level = 0
          for (let k = 1; k < levels.length; k++) {
            if (v >= levels[k] - 0.13) level = levels[k]
          }
          if (!level) continue
          const radius = R * (level === 0.26 ? 0.38 : level)
          ctx.beginPath()
          ctx.arc((i + 0.5) * step, (j + 0.5) * rowStep, radius, 0, Math.PI * 2)
          ctx.fill()
        }
      }
    }

    img.onload = () => {
      size()
      makeTiles()
    }
    img.onerror = () => {
      console.warn('Halftone sheet image fallback active.')
    }
    img.src = SHEET_URL

    size()
    window.addEventListener('resize', size, { passive: true })

    let animId: number
    const start = performance.now()
    function frame(now: number) {
      render((now - start) / 1000)
      animId = requestAnimationFrame(frame)
    }
    animId = requestAnimationFrame(frame)

    return () => {
      cancelAnimationFrame(animId)
      window.removeEventListener('resize', size)
    }
  }, [])

  return <canvas id="dots" ref={canvasRef} aria-hidden="true" />
}
