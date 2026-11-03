import React, { useState, useEffect } from 'react'

interface HardwareSpecs {
  sessionCode?: string
  manufacturer: string
  model: string
  serialNumber: string
  cpu: string
  ram: string
  storage: string
  operatingSystem: string
}

export function RepairForm() {
  const [sessionCode] = useState(() => 'TC-' + Math.floor(1000 + Math.random() * 9000))
  const [isListening, setIsListening] = useState(false)
  const [autoFilled, setAutoFilled] = useState(false)

  // Form State
  const [formData, setFormData] = useState({
    customerName: '',
    customerPhone: '',
    customerEmail: '',
    modelName: '',
    serialNumber: '',
    cpu: '',
    ram: '',
    storage: '',
    operatingSystem: '',
    issueDescription: '',
  })

  const [submitting, setSubmitting] = useState(false)
  const [submitResult, setSubmitResult] = useState<{ success: boolean; code?: string; msg?: string } | null>(null)

  // Polling listener when user triggers auto-scan
  useEffect(() => {
    if (!isListening || !sessionCode) return

    const interval = setInterval(async () => {
      try {
        const res = await fetch(`http://localhost:5000/api/v1/diagnostics/quick-sync/${sessionCode}`)
        const data = await res.json()
        if (data && data.synced && data.data) {
          const spec: HardwareSpecs = data.data
          setFormData((prev) => ({
            ...prev,
            modelName: spec.model || prev.modelName,
            serialNumber: spec.serialNumber || prev.serialNumber,
            cpu: spec.cpu || prev.cpu,
            ram: spec.ram || prev.ram,
            storage: spec.storage || prev.storage,
            operatingSystem: spec.operatingSystem || prev.operatingSystem,
          }))

          setIsListening(false)
          setAutoFilled(true)
          setTimeout(() => setAutoFilled(false), 3000)
        }
      } catch (err) {
        console.warn('Sync polling waiting...', err)
      }
    }, 1500)

    return () => clearInterval(interval)
  }, [isListening, sessionCode])

  const handleStartScan = () => {
    setIsListening(true)
    // Trigger download of pre-configured 1-click batch script
    const downloadUrl = `http://localhost:5000/api/v1/diagnostics/download-script/${sessionCode}`
    const a = document.createElement('a')
    a.href = downloadUrl
    a.download = `TechCare-Scan-${sessionCode}.bat`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
  }

  const handlePasteClipboard = async () => {
    try {
      const text = await navigator.clipboard.readText()
      if (!text) {
        alert('Bộ nhớ tạm (Clipboard) đang trống!')
        return
      }

      const lines = text.split('\n')
      const specs: Record<string, string> = {}
      lines.forEach((l) => {
        const [k, ...v] = l.split(':')
        if (k && v) specs[k.trim()] = v.join(':').trim()
      })

      if (specs['Model'] || specs['CPU'] || specs['RAM']) {
        setFormData((prev) => ({
          ...prev,
          modelName: specs['Model'] || prev.modelName,
          serialNumber: specs['Serial'] || prev.serialNumber,
          cpu: specs['CPU'] || prev.cpu,
          ram: specs['RAM'] || prev.ram,
          storage: specs['Ổ cứng'] || prev.storage,
          operatingSystem: specs['Hệ điều hành'] || prev.operatingSystem,
        }))
        setAutoFilled(true)
        setTimeout(() => setAutoFilled(false), 3000)
      } else {
        alert('Dữ liệu clipboard chưa đúng định dạng quét cấu hình.')
      }
    } catch {
      alert('Vui lòng cho phép quyền truy cập Clipboard trên trình duyệt.')
    }
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    setSubmitResult(null)

    try {
      const payload = {
        customerName: formData.customerName.trim(),
        customerPhone: formData.customerPhone.trim(),
        customerEmail: formData.customerEmail?.trim() || null,
        modelName: formData.modelName?.trim() || null,
        serialNumber: formData.serialNumber?.trim() || null,
        cpu: formData.cpu?.trim() || null,
        ram: formData.ram?.trim() || null,
        storage: formData.storage?.trim() || null,
        operatingSystem: formData.operatingSystem?.trim() || null,
        issueDescription: formData.issueDescription.trim(),
      }

      const res = await fetch('http://localhost:5000/api/v1/appointments/submit', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json; charset=utf-8',
        },
        body: JSON.stringify(payload),
      })

      const data = await res.json()
      if (res.ok && data.success) {
        setSubmitResult({
          success: true,
          code: data.appointmentCode,
          msg: 'Đã lưu lịch hẹn và thông tin máy vào hệ thống SQL Server TechcareWed!',
        })
      } else {
        setSubmitResult({
          success: false,
          msg: data.message || 'Lỗi kiểm tra thông tin hoặc máy chủ.',
        })
      }
    } catch {
      setSubmitResult({
        success: false,
        msg: 'Không thể kết nối đến Backend API (localhost:5000). Vui lòng thử lại sau.',
      })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <form className="booking-form" onSubmit={handleSubmit}>
      {/* 1. Quick-Scan Banner */}
      <div className="scan-banner">
        <div className="scan-banner-info">
          <h4>⚡ Quét cấu hình máy 1-Click</h4>
          <p>
            {isListening
              ? `Đang lắng nghe cấu hình (Mã: ${sessionCode})...`
              : 'Tải script quét để tự động điền CPU, RAM, Model'}
          </p>
        </div>
        <div style={{ display: 'flex', gap: '8px' }}>
          <button
            type="button"
            className={`scan-btn ${isListening ? 'scanning' : ''}`}
            onClick={handleStartScan}
          >
            {isListening ? 'Đang đợi đồng bộ...' : 'Quét tự động (.bat)'}
          </button>
          <button
            type="button"
            className="scan-btn"
            style={{ background: '#e3e2dd', color: '#111' }}
            onClick={handlePasteClipboard}
            title="Dán nhanh nếu đã chạy script PowerShell"
          >
            Dán Clipboard
          </button>
        </div>
      </div>

      {/* 2. Customer Fields */}
      <div className="form-row-2">
        <div>
          <label>Họ và tên *</label>
          <input
            type="text"
            required
            placeholder="Ví dụ: Nguyễn Văn An"
            value={formData.customerName}
            onChange={(e) => setFormData({ ...formData, customerName: e.target.value })}
          />
        </div>
        <div>
          <label>Số điện thoại *</label>
          <input
            type="tel"
            required
            placeholder="Ví dụ: 0912345678"
            value={formData.customerPhone}
            onChange={(e) => setFormData({ ...formData, customerPhone: e.target.value })}
          />
        </div>
      </div>

      <div className="form-row-2">
        <div>
          <label>Email liên hệ</label>
          <input
            type="email"
            placeholder="Ví dụ: an.nguyen@email.com"
            value={formData.customerEmail}
            onChange={(e) => setFormData({ ...formData, customerEmail: e.target.value })}
          />
        </div>
        <div>
          <label>Model máy (Tự động hoặc nhập tay)</label>
          <input
            type="text"
            placeholder="Ví dụ: ThinkPad X1 Carbon"
            className={autoFilled ? 'auto-filled-highlight' : ''}
            value={formData.modelName}
            onChange={(e) => setFormData({ ...formData, modelName: e.target.value })}
          />
        </div>
      </div>

      {/* 3. Specs Fields */}
      <div className="form-row-2">
        <div>
          <label>Vi xử lý (CPU)</label>
          <input
            type="text"
            placeholder="Ví dụ: Intel Core i7-13700H"
            className={autoFilled ? 'auto-filled-highlight' : ''}
            value={formData.cpu}
            onChange={(e) => setFormData({ ...formData, cpu: e.target.value })}
          />
        </div>
        <div>
          <label>Bộ nhớ (RAM)</label>
          <input
            type="text"
            placeholder="Ví dụ: 16 GB DDR5"
            className={autoFilled ? 'auto-filled-highlight' : ''}
            value={formData.ram}
            onChange={(e) => setFormData({ ...formData, ram: e.target.value })}
          />
        </div>
      </div>

      <div className="form-row-2">
        <div>
          <label>Ổ cứng (SSD / HDD)</label>
          <input
            type="text"
            placeholder="Ví dụ: 512GB NVMe SSD"
            className={autoFilled ? 'auto-filled-highlight' : ''}
            value={formData.storage}
            onChange={(e) => setFormData({ ...formData, storage: e.target.value })}
          />
        </div>
        <div>
          <label>Hệ điều hành</label>
          <input
            type="text"
            placeholder="Ví dụ: Windows 11 Pro"
            className={autoFilled ? 'auto-filled-highlight' : ''}
            value={formData.operatingSystem}
            onChange={(e) => setFormData({ ...formData, operatingSystem: e.target.value })}
          />
        </div>
      </div>

      {/* 4. Issue Description */}
      <div>
        <label>Tình trạng lỗi cần sửa chữa *</label>
        <textarea
          rows={3}
          required
          placeholder="Mô tả hiện tượng: bật không lên nguồn, màn hình sọc, quạt kêu to, v.v."
          value={formData.issueDescription}
          onChange={(e) => setFormData({ ...formData, issueDescription: e.target.value })}
        />
      </div>

      {/* 5. Submit Button */}
      <button type="submit" className="button submit-button" disabled={submitting}>
        {submitting ? 'Đang gửi yêu cầu...' : 'Gửi yêu cầu kiểm tra máy'} <span>→</span>
      </button>

      {/* 6. Form Feedback Note */}
      {submitResult ? (
        <div className={`form-note ${submitResult.success ? 'success' : ''}`}>
          {submitResult.success ? (
            <div>
              <strong>✓ ĐẶT LỊCH THÀNH CÔNG!</strong> Mã phiếu:{' '}
              <span style={{ textDecoration: 'underline', fontWeight: 600 }}>{submitResult.code}</span>
              <p style={{ marginTop: '4px', fontSize: '11px', color: '#065f46' }}>{submitResult.msg}</p>
            </div>
          ) : (
            <span style={{ color: '#dc2626' }}>⚠ {submitResult.msg}</span>
          )}
        </div>
      ) : (
        <div className="form-note">
          Yêu cầu sẽ được gửi trực tiếp đến hệ thống tiếp nhận TechCare. Kỹ thuật viên sẽ liên hệ xác nhận trong vòng 15 phút.
        </div>
      )}
    </form>
  )
}
