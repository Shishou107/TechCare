import React, { useState, useEffect } from 'react'
import { API_BASE_URL } from '../api/client'

export interface DiskSerialInfo {
  model?: string
  serialNumber?: string | null
  sizeGB?: number
}

export interface RamModuleInfo {
  serialNumber?: string | null
  partNumber?: string | null
  capacityGB?: number
}

export interface PublicTechnician {
  profileId: number
  userId: number
  displayName: string
  avatarUrl?: string
  specialty: string
  bio?: string
  experienceYears: number
  isAcceptingRequests: boolean
  displayOrder: number
  ratingAverage: number | null
  ratingCount: number
  completedTicketsCount: number
}

export interface HardwareSpecs {
  sessionCode?: string
  manufacturer: string
  model: string
  serialNumber: string
  cpu: string
  ram: string
  storage: string
  operatingSystem: string
  diskSerials?: DiskSerialInfo[]
  ramModules?: RamModuleInfo[]
}

interface BookingSectionProps {
  selectedIssue?: string
  onNavigateToTracking?: (code: string, phone: string) => void
}

function CheckIcon() {
  return (
    <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3.2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="20 6 9 17 4 12" />
    </svg>
  )
}

export function BookingSection({ selectedIssue = '', onNavigateToTracking }: BookingSectionProps) {
  // Cryptographic session code for scan syncing
  const [sessionCode] = useState(() => {
    if (typeof crypto !== 'undefined' && crypto.getRandomValues) {
      const bytes = new Uint8Array(8)
      crypto.getRandomValues(bytes)
      return 'TC-' + Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('').toUpperCase()
    }
    return 'TC-' + Math.random().toString(36).slice(2, 14).toUpperCase()
  })

  const [isListening, setIsListening] = useState(false)
  const [autoFilled, setAutoFilled] = useState(false)
  const [showManualSpecs, setShowManualSpecs] = useState(false)
  const [scanTimestamp, setScanTimestamp] = useState<string | null>(null)
  const [technicians, setTechnicians] = useState<PublicTechnician[]>([])
  const [selectedTechId, setSelectedTechId] = useState<number | null>(null)

  useEffect(() => {
    fetch(`${API_BASE_URL}/api/v1/technicians/public`)
      .then((res) => (res.ok ? res.json() : []))
      .then((data: PublicTechnician[]) => setTechnicians(data))
      .catch(() => {})
  }, [])

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
    diskSerials: [] as DiskSerialInfo[],
    ramModules: [] as RamModuleInfo[],
    issueDescription: selectedIssue,
    serviceType: 'AT_STORE' as 'AT_STORE' | 'PICKUP' | 'COURIER',
    // Courier options
    courierCarrier: 'GHN' as 'GHN' | 'GHTK' | 'VIETTEL',
    province: 'Hà Nội',
    district: 'Quận Cầu Giấy',
    ward: 'Phường Dịch Vọng Hậu',
    streetAddress: '',
    insuredLaptop: true,
  })

  const [errors, setErrors] = useState<{ [key: string]: string }>({})
  const [submitting, setSubmitting] = useState(false)
  const [submitResult, setSubmitResult] = useState<{ success: boolean; code?: string; msg?: string; carrier?: string } | null>(null)

  const [shippingPreview, setShippingPreview] = useState<{ fee: number; leadTime: string; loading: boolean }>({
    fee: 35000,
    leadTime: '2–3 ngày',
    loading: false,
  })

  // Dynamic GHN Fee Preview Calculation
  useEffect(() => {
    if (formData.serviceType !== 'COURIER') return

    let active = true
    setShippingPreview((prev) => ({ ...prev, loading: true }))

    const timer = setTimeout(async () => {
      try {
        const res = await fetch(`${API_BASE_URL}/api/v1/shipping/fee-preview`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            toProvince: formData.province,
            toDistrict: formData.district,
            toWard: formData.ward,
            weightGrams: 2500,
            insuranceValue: formData.insuredLaptop ? 15000000 : 0,
          }),
        })
        if (res.ok && active) {
          const data = await res.json()
          setShippingPreview({
            fee: data.totalFee || 35000,
            leadTime: data.expectedDeliveryDate ? new Date(data.expectedDeliveryDate).toLocaleDateString('vi-VN') : '2–3 ngày',
            loading: false,
          })
        }
      } catch {
        if (active) setShippingPreview((prev) => ({ ...prev, loading: false }))
      }
    }, 400)

    return () => {
      active = false
      clearTimeout(timer)
    }
  }, [formData.serviceType, formData.province, formData.district, formData.ward, formData.insuredLaptop])

  // Sync issue description when selected from symptoms or services
  useEffect(() => {
    if (selectedIssue) {
      setFormData((prev) => ({
        ...prev,
        issueDescription: selectedIssue,
      }))
    }
  }, [selectedIssue])

  // Polling listener for 1-Click quick-scan
  useEffect(() => {
    if (!isListening || !sessionCode) return

    const interval = setInterval(async () => {
      try {
        const res = await fetch(`${API_BASE_URL}/api/v1/diagnostics/quick-sync/${sessionCode}`)
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
            diskSerials: spec.diskSerials || prev.diskSerials,
            ramModules: spec.ramModules || prev.ramModules,
          }))

          setIsListening(false)
          setAutoFilled(true)
          setScanTimestamp(new Date().toLocaleTimeString('vi-VN'))
          setShowManualSpecs(false)
          setTimeout(() => setAutoFilled(false), 3500)
        }
      } catch (err) {
        console.warn('Sync polling waiting...', err)
      }
    }, 1500)

    return () => clearInterval(interval)
  }, [isListening, sessionCode])

  const handleStartScan = () => {
    setIsListening(true)
    const downloadUrl = `${API_BASE_URL}/api/v1/diagnostics/download-script/${sessionCode}`
    const a = document.createElement('a')
    a.href = downloadUrl
    a.download = `TechCare-Scan-${sessionCode}.bat`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
  }

  const handleDownloadPs1 = () => {
    setIsListening(true)
    const downloadUrl = `${API_BASE_URL}/api/v1/diagnostics/download-script/${sessionCode}?format=ps1`
    const a = document.createElement('a')
    a.href = downloadUrl
    a.download = `TechCare-Scan-${sessionCode}.ps1`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
  }

  const validate = () => {
    const errs: { [key: string]: string } = {}
    if (!formData.customerName.trim()) {
      errs.customerName = 'Vui lòng nhập họ và tên của bạn'
    }
    if (!formData.customerPhone.trim()) {
      errs.customerPhone = 'Vui lòng nhập số điện thoại'
    } else if (!/(0[3|5|7|8|9])+([0-9]{8})\b/.test(formData.customerPhone.trim())) {
      errs.customerPhone = 'Số điện thoại không hợp lệ (cần 10 chữ số)'
    }
    if (!formData.issueDescription.trim()) {
      errs.issueDescription = 'Vui lòng mô tả hiện tượng máy bị lỗi'
    }
    if ((formData.serviceType === 'COURIER' || formData.serviceType === 'PICKUP') && !formData.streetAddress.trim()) {
      errs.streetAddress = 'Vui lòng nhập số nhà, tên đường để giao nhận thiết bị'
    } else if ((formData.serviceType === 'COURIER' || formData.serviceType === 'PICKUP') && formData.streetAddress.trim().length < 5) {
      errs.streetAddress = 'Địa chỉ quá ngắn (vui lòng nhập số nhà, tên đường chi tiết)'
    }

    setErrors(errs)
    return Object.keys(errs).length === 0
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!validate()) return

    setSubmitting(true)
    setSubmitResult(null)

    try {
      let courierNotes = ''
      if (formData.serviceType === 'COURIER') {
        courierNotes = `\n[GỬI CHUYỂN PHÁT ${formData.courierCarrier}] Lấy hàng tại: ${formData.streetAddress.trim()}, ${formData.ward}, ${formData.district}, ${formData.province}. Bảo hiểm laptop: ${formData.insuredLaptop ? 'CÓ' : 'KHÔNG'}`
      } else if (formData.serviceType === 'PICKUP') {
        courierNotes = `\n[GIAO NHẬN TẬN NƠI] Lấy máy tại: ${formData.streetAddress.trim()}, ${formData.ward}, ${formData.district}, ${formData.province}`
      }

      const fullPickupAddress = (formData.serviceType === 'COURIER' || formData.serviceType === 'PICKUP')
        ? `${formData.streetAddress.trim()}, ${formData.ward}, ${formData.district}, ${formData.province}`
        : null

      const servicePrefix =
        formData.serviceType === 'COURIER'
          ? `GỬI CHUYỂN PHÁT (${formData.courierCarrier})`
          : formData.serviceType === 'PICKUP'
          ? 'GIAO NHẬN TẬN NƠI'
          : 'TẠI TIỆM'

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
        diskSerialsJson: formData.diskSerials && formData.diskSerials.length > 0 ? JSON.stringify(formData.diskSerials) : null,
        ramModulesJson: formData.ramModules && formData.ramModules.length > 0 ? JSON.stringify(formData.ramModules) : null,
        issueDescription: `[${servicePrefix}] ${formData.issueDescription.trim()}${courierNotes}`,
        preferredTechnicianUserId: selectedTechId,
        deliveryMethod: formData.serviceType === 'COURIER' ? 'SHIP_IN' : formData.serviceType,
        pickupAddress: fullPickupAddress,
      }

      const res = await fetch(`${API_BASE_URL}/api/v1/appointments/book`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      })

      const data = await res.json()

      if (res.ok && data.success) {
        setSubmitResult({
          success: true,
          code: data.appointmentCode,
          carrier: formData.serviceType === 'COURIER' ? formData.courierCarrier : undefined,
        })
      } else {
        setSubmitResult({
          success: false,
          msg: data.message || 'Đã có lỗi xảy ra khi gửi yêu cầu. Vui lòng thử lại.',
        })
      }
    } catch {
      setSubmitResult({
        success: false,
        msg: 'Lỗi mạng hoặc không thể kết nối tới máy chủ. Vui lòng thử lại sau.',
      })
    } finally {
      setSubmitting(false)
    }
  }

  const handleReset = () => {
    setSubmitResult(null)
    setFormData({
      customerName: '',
      customerPhone: '',
      customerEmail: '',
      modelName: '',
      serialNumber: '',
      cpu: '',
      ram: '',
      storage: '',
      operatingSystem: '',
      diskSerials: [],
      ramModules: [],
      issueDescription: '',
      serviceType: 'AT_STORE',
      courierCarrier: 'GHN',
      province: 'Hà Nội',
      district: 'Quận Cầu Giấy',
      ward: 'Phường Dịch Vọng Hậu',
      streetAddress: '',
      insuredLaptop: true,
    })
    setScanTimestamp(null)
  }

  const hasHardwareData = Boolean(
    formData.serialNumber ||
    formData.modelName ||
    (formData.diskSerials && formData.diskSerials.length > 0) ||
    (formData.ramModules && formData.ramModules.length > 0)
  )

  return (
    <section id="booking-section" className="booking-section">
      <div className="booking-container-photo1">
        {/* Left Column: Heading and info */}
        <div className="booking-left-copy">
          <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '8px' }}>
            TIẾP NHẬN YÊU CẦU TRỰC TUYẾN
          </div>
          <h2>Đặt lịch kiểm tra máy ngay.</h2>
          <p className="booking-lead-text">
            Chỉ cần 30 giây để gửi thông tin. Kỹ thuật viên TechCare sẽ liên hệ xác nhận, chuẩn bị sẵn linh kiện và hỗ trợ kiểm tra trực tiếp tại tiệm, đến nhận tận nơi hoặc tiếp nhận gửi chuyển phát toàn quốc.
          </p>

          <div className="booking-online-badge">
            <span className="dot"></span>
            <span>Hệ thống trực tuyến tiếp nhận 24/7 | Chi nhánh TechCare</span>
          </div>

          <div className="booking-sidebar-note">
            <h4>Minh bạch linh kiện & Chống tráo đổi</h4>
            <p>
              Tính năng Quét 1-Click tự động trích xuất Serial máy, Serial ổ cứng SSD/HDD và từng thanh RAM vào biên nhận. Khách hàng và kỹ thuật viên đều có bằng chứng đối chiếu trước & sau sửa.
            </p>
          </div>
        </div>

        {/* Right Column: 2-Step Form */}
        <div className="booking-right-form">
          {submitResult?.success ? (
            <div className="modal-success-box">
              <div className="success-icon-mono">
                ✓
              </div>
              <h3 style={{ fontSize: '20px', fontWeight: 600, marginBottom: '8px' }}>ĐẶT LỊCH THÀNH CÔNG!</h3>
              
              <div className="success-code-card">
                <div className="success-code-label">Mã phiếu hẹn / Mã tra cứu</div>
                <div className="success-code-val">
                  {submitResult.code}
                </div>
                {submitResult.carrier && (
                  <div className="success-carrier-sub">
                    Đơn vị vận chuyển đối tác: {submitResult.carrier} Express (Toàn quốc)
                  </div>
                )}
              </div>

              <p style={{ fontSize: '13.5px', color: '#555', lineHeight: 1.6, maxWidth: '480px', margin: '0 auto 24px' }}>
                Kỹ thuật viên của TechCare sẽ liên hệ lại qua số <strong>{formData.customerPhone}</strong> trong vòng 15 phút để xác nhận và chuẩn bị phương án tiếp nhận.
              </p>

              <div style={{ display: 'flex', gap: '12px', justifyContent: 'center', flexWrap: 'wrap' }}>
                {onNavigateToTracking && submitResult.code && (
                  <button
                    type="button"
                    className="button"
                    onClick={() => onNavigateToTracking(submitResult.code!, formData.customerPhone)}
                  >
                    Theo dõi hành trình <span>→</span>
                  </button>
                )}
                <button
                  type="button"
                  className="button"
                  style={{ background: '#ffffff', color: '#111', border: '1px solid #111' }}
                  onClick={handleReset}
                >
                  Đặt lịch thiết bị khác
                </button>
              </div>
            </div>
          ) : (
            <form className="booking-form booking-stepper-container" onSubmit={handleSubmit} noValidate>
              
              {/* STEP 1: SERVICE TYPE & CONTACT INFO */}
              <div className="booking-step-group step-1-group">
                <div className="step-header-row">
                  <div className="step-num-circle">1</div>
                  <div className="step-header-text">
                    <span className="step-title">Thông tin tiếp nhận</span>
                    <span className="step-subtitle">Chọn hình thức phục vụ và thông tin liên hệ</span>
                  </div>
                </div>

                {/* 3 Service Type Option Cards (Equal height, pure black & white, no icon boxes) */}
                <div className="service-cards-grid">
                  {/* Card 1: Tại cửa hàng */}
                  <div
                    className={`service-option-card ${formData.serviceType === 'AT_STORE' ? 'selected' : ''}`}
                    onClick={() => setFormData({ ...formData, serviceType: 'AT_STORE' })}
                  >
                    <div className="card-top-row">
                      <h4 className="card-title">Tại cửa hàng</h4>
                      <div className={`card-check-pill ${formData.serviceType === 'AT_STORE' ? 'checked' : ''}`}>
                        {formData.serviceType === 'AT_STORE' ? <CheckIcon /> : null}
                      </div>
                    </div>
                    <div className="card-fact-text">Miễn phí kiểm tra · 30–45 phút</div>
                    <p className="card-desc">Kỹ thuật viên đo đạc, kiểm tra trực tiếp trước mặt khách.</p>
                  </div>

                  {/* Card 2: Kỹ thuật đến tận nơi */}
                  <div
                    className={`service-option-card ${formData.serviceType === 'PICKUP' ? 'selected' : ''}`}
                    onClick={() => setFormData({ ...formData, serviceType: 'PICKUP' })}
                  >
                    <div className="card-top-row">
                      <h4 className="card-title">Kỹ thuật đến tận nơi</h4>
                      <div className={`card-check-pill ${formData.serviceType === 'PICKUP' ? 'checked' : ''}`}>
                        {formData.serviceType === 'PICKUP' ? <CheckIcon /> : null}
                      </div>
                    </div>
                    <div className="card-fact-text">Nội thành · Phí thu hộ 30–50k</div>
                    <p className="card-desc">Nhận máy tận nhà (1–2h), xuất biên nhận điện tử minh bạch.</p>
                  </div>

                  {/* Card 3: Gửi chuyển phát toàn quốc */}
                  <div
                    className={`service-option-card ${formData.serviceType === 'COURIER' ? 'selected' : ''}`}
                    onClick={() => setFormData({ ...formData, serviceType: 'COURIER' })}
                  >
                    <div className="card-top-row">
                      <h4 className="card-title">Gửi chuyển phát</h4>
                      <div className={`card-check-pill ${formData.serviceType === 'COURIER' ? 'checked' : ''}`}>
                        {formData.serviceType === 'COURIER' ? <CheckIcon /> : null}
                      </div>
                    </div>
                    <div className="card-fact-text">Toàn quốc · Đóng gói chống sốc</div>
                    <p className="card-desc">Liên tỉnh qua GHN / GHTK / Viettel Post, bảo hiểm toàn phần.</p>
                  </div>
                </div>

                {/* Additional Shipping & Address Fields when COURIER or PICKUP selected */}
                {(formData.serviceType === 'COURIER' || formData.serviceType === 'PICKUP') && (
                  <div className="courier-shipping-drawer">
                    <div className="courier-drawer-header">
                      <span className="courier-drawer-title">
                        {formData.serviceType === 'PICKUP'
                          ? 'Địa chỉ kỹ thuật viên TechCare đến nhận máy tận nơi'
                          : 'Địa chỉ lấy máy & Đơn vị vận chuyển đối tác'}
                      </span>
                    </div>

                    <div className="form-row-2">
                      <div>
                        <label>Tỉnh / Thành phố *</label>
                        <select
                          value={formData.province}
                          onChange={(e) => setFormData({ ...formData, province: e.target.value })}
                          className="courier-select"
                        >
                          <option value="Hà Nội">Hà Nội</option>
                          <option value="TP. Hồ Chí Minh">TP. Hồ Chí Minh</option>
                          <option value="Đà Nẵng">Đà Nẵng</option>
                          <option value="Hải Phòng">Hải Phòng</option>
                          <option value="Cần Thơ">Cần Thơ</option>
                          <option value="Bình Dương">Bình Dương</option>
                          <option value="Đồng Nai">Đồng Nai</option>
                        </select>
                      </div>
                      <div>
                        <label>Quận / Huyện *</label>
                        <select
                          value={formData.district}
                          onChange={(e) => setFormData({ ...formData, district: e.target.value })}
                          className="courier-select"
                        >
                          <option value="Quận Cầu Giấy">Quận Cầu Giấy</option>
                          <option value="Quận Đống Đa">Quận Đống Đa</option>
                          <option value="Quận 1">Quận 1</option>
                          <option value="Quận 3">Quận 3</option>
                          <option value="Quận Bình Thạnh">Quận Bình Thạnh</option>
                          <option value="Quận Hải Châu">Quận Hải Châu</option>
                          <option value="Huyện / Thị xã khác">Huyện / Thị xã khác</option>
                        </select>
                      </div>
                    </div>

                    <div className="form-row-2">
                      <div>
                        <label>Phường / Xã *</label>
                        <input
                          type="text"
                          placeholder="Ví dụ: Phường Dịch Vọng Hậu"
                          value={formData.ward}
                          onChange={(e) => setFormData({ ...formData, ward: e.target.value })}
                        />
                      </div>
                      <div>
                        <label>Số nhà, Tên đường (Chi tiết) *</label>
                        <input
                          type="text"
                          placeholder="Ví dụ: Số 25 Ngõ 102 Trần Thái Tông"
                          value={formData.streetAddress}
                          onChange={(e) => {
                            setFormData({ ...formData, streetAddress: e.target.value })
                            if (errors.streetAddress) setErrors({ ...errors, streetAddress: '' })
                          }}
                          className={errors.streetAddress ? 'input-error' : ''}
                        />
                        {errors.streetAddress && <span className="field-error-msg">{errors.streetAddress}</span>}
                      </div>
                    </div>

                    {/* Carrier comparison list (only for COURIER) */}
                    {formData.serviceType === 'COURIER' && (
                      <>
                        <div style={{ marginTop: '10px', marginBottom: '12px' }}>
                          <label style={{ marginBottom: '8px' }}>Chọn đơn vị chuyển phát đối tác:</label>
                          <div className="carrier-list-box">
                            <label className={`carrier-item ${formData.courierCarrier === 'GHN' ? 'active' : ''}`}>
                              <input
                                type="radio"
                                name="courierCarrier"
                                checked={formData.courierCarrier === 'GHN'}
                                onChange={() => setFormData({ ...formData, courierCarrier: 'GHN' })}
                              />
                              <div className="carrier-info">
                                <strong>Giao Hàng Nhanh (GHN Express)</strong>
                                <span>
                                  {shippingPreview.loading
                                    ? 'Đang tính cước theo địa chỉ...'
                                    : `Dự kiến: ${shippingPreview.leadTime} · Phí cước tự động: ${shippingPreview.fee.toLocaleString('vi-VN')}đ`}
                                </span>
                              </div>
                              <span className="carrier-tag-mono">Đề xuất</span>
                            </label>

                            <label className={`carrier-item ${formData.courierCarrier === 'GHTK' ? 'active' : ''}`}>
                              <input
                                type="radio"
                                name="courierCarrier"
                                checked={formData.courierCarrier === 'GHTK'}
                                onChange={() => setFormData({ ...formData, courierCarrier: 'GHTK' })}
                              />
                              <div className="carrier-info">
                                <strong>Giao Hàng Tiết Kiệm (GHTK)</strong>
                                <span>Dự kiến: 3–4 ngày · Phí cước ước tính: 30.000đ</span>
                              </div>
                            </label>

                            <label className={`carrier-item ${formData.courierCarrier === 'VIETTEL' ? 'active' : ''}`}>
                              <input
                                type="radio"
                                name="courierCarrier"
                                checked={formData.courierCarrier === 'VIETTEL'}
                                onChange={() => setFormData({ ...formData, courierCarrier: 'VIETTEL' })}
                              />
                              <div className="carrier-info">
                                <strong>Viettel Post</strong>
                                <span>Dự kiến: 2–3 ngày · Phí cước ước tính: 38.000đ</span>
                              </div>
                            </label>
                          </div>
                        </div>

                        {/* Insurance & Anti-shock Packaging reminder */}
                        <div className="courier-notice-box">
                          <label className="checkbox-label" style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer', fontSize: '12.5px', fontWeight: 600, color: 'var(--ink)' }}>
                            <input
                              type="checkbox"
                              checked={formData.insuredLaptop}
                              onChange={(e) => setFormData({ ...formData, insuredLaptop: e.target.checked })}
                            />
                            <span>Khai giá & Bảo hiểm toàn phần kiện hàng Laptop (TechCare hỗ trợ thủ tục với hãng vận chuyển)</span>
                          </label>
                          <p style={{ fontSize: '11.5px', color: '#666', marginTop: '6px', lineHeight: 1.5 }}>
                            <strong>Lưu ý đóng gói an toàn:</strong> Vui lòng bọc thiết bị tối thiểu 3–4 lớp xốp khí chống sốc và chèn cố định trong thùng carton trước khi bàn giao cho bưu tá.
                          </p>
                        </div>
                      </>
                    )}
                  </div>
                )}

                {/* Name & Phone */}
                <div className="form-row-2" style={{ marginTop: '16px' }}>
                  <div>
                    <label>Họ và tên *</label>
                    <input
                      type="text"
                      placeholder="Ví dụ: Nguyễn Văn An"
                      value={formData.customerName}
                      onChange={(e) => {
                        setFormData({ ...formData, customerName: e.target.value })
                        if (errors.customerName) setErrors({ ...errors, customerName: '' })
                      }}
                      className={errors.customerName ? 'input-error' : ''}
                    />
                    {errors.customerName && <span className="field-error-msg">{errors.customerName}</span>}
                  </div>
                  <div>
                    <label>Số điện thoại *</label>
                    <input
                      type="tel"
                      placeholder="Ví dụ: 0912345678"
                      value={formData.customerPhone}
                      onChange={(e) => {
                        setFormData({ ...formData, customerPhone: e.target.value })
                        if (errors.customerPhone) setErrors({ ...errors, customerPhone: '' })
                      }}
                      className={errors.customerPhone ? 'input-error' : ''}
                    />
                    {errors.customerPhone && <span className="field-error-msg">{errors.customerPhone}</span>}
                  </div>
                </div>

                {/* Issue Description */}
                <div>
                  <label>Tình trạng lỗi cần kiểm tra & sửa chữa *</label>
                  <textarea
                    rows={3}
                    placeholder="Mô tả hiện tượng: bật không lên nguồn, sọc màn hình, quạt kêu to, đổ nước, mất mạng..."
                    value={formData.issueDescription}
                    onChange={(e) => {
                      setFormData({ ...formData, issueDescription: e.target.value })
                      if (errors.issueDescription) setErrors({ ...errors, issueDescription: '' })
                    }}
                    className={errors.issueDescription ? 'input-error' : ''}
                  />
                  {errors.issueDescription && <span className="field-error-msg">{errors.issueDescription}</span>}
                </div>

                {submitResult && !submitResult.success && (
                  <div style={{ color: '#dc2626', fontSize: '12.5px', marginBottom: '14px', background: '#fee2e2', padding: '10px 14px', borderRadius: '8px' }}>
                    {submitResult.msg}
                  </div>
                )}

                {/* PREFERRED TECHNICIAN SELECTION (Phase 5B / Phase 6B) */}
                <div style={{ marginTop: '20px', marginBottom: '18px' }}>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>
                    Kỹ thuật viên yêu cầu (Tùy chọn)
                  </label>
                  <p style={{ fontSize: '12px', color: 'var(--sub)', margin: '0 0 10px 0' }}>
                    Chọn chuyên gia sửa chữa máy của bạn. Đây là nguyện vọng — nếu kỹ thuật viên bận, lễ tân sẽ sắp xếp nhân sự có chuyên môn tương đương.
                  </p>

                  {technicians.length === 0 ? (
                    <div style={{ padding: '14px 16px', background: 'var(--bg)', borderRadius: '8px', fontSize: '12.5px', color: 'var(--sub)', border: '1px solid var(--line)' }}>
                      ⚙️ TechCare sẽ chủ động sắp xếp kỹ thuật viên phù hợp nhất với dòng máy của bạn.
                    </div>
                  ) : (
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(230px, 1fr))', gap: '10px' }}>
                      {/* Default Option: Để TechCare sắp xếp */}
                      <div
                        onClick={() => setSelectedTechId(null)}
                        style={{
                          border: selectedTechId === null ? '2px solid var(--ink)' : '1px solid var(--line)',
                          borderRadius: '8px',
                          padding: '12px 14px',
                          cursor: 'pointer',
                          background: selectedTechId === null ? '#fafafa' : '#fff',
                          display: 'flex',
                          alignItems: 'center',
                          gap: '10px'
                        }}
                      >
                        <div style={{ width: '38px', height: '38px', borderRadius: '50%', background: '#eee', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '16px' }}>
                          ⚙️
                        </div>
                        <div>
                          <div style={{ fontWeight: 600, fontSize: '13px' }}>Để TechCare sắp xếp</div>
                          <div style={{ fontSize: '11px', color: 'var(--sub)' }}>Ưu tiên nhận máy nhanh nhất</div>
                        </div>
                      </div>

                      {/* Available Technicians */}
                      {technicians.map((tech) => (
                        <div
                          key={tech.userId}
                          onClick={() => setSelectedTechId(tech.userId)}
                          style={{
                            border: selectedTechId === tech.userId ? '2px solid var(--ink)' : '1px solid var(--line)',
                            borderRadius: '8px',
                            padding: '12px 14px',
                            cursor: 'pointer',
                            background: selectedTechId === tech.userId ? '#fafafa' : '#fff',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '10px',
                            opacity: tech.isAcceptingRequests ? 1 : 0.6
                          }}
                        >
                          <img
                            src={tech.avatarUrl || 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&q=80&w=120'}
                            alt={tech.displayName}
                            style={{ width: '38px', height: '38px', borderRadius: '50%', objectFit: 'cover' }}
                          />
                          <div style={{ flex: 1, minWidth: 0 }}>
                            <div style={{ fontWeight: 600, fontSize: '13px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                              <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{tech.displayName}</span>
                              {tech.ratingAverage !== null && tech.ratingCount > 0 ? (
                                <span style={{ fontSize: '11px', color: '#eab308' }}>★ {Number(tech.ratingAverage).toFixed(1)}</span>
                              ) : (
                                <span style={{ fontSize: '10.5px', color: 'var(--sub)' }}>Chưa có đánh giá</span>
                              )}
                            </div>
                            <div style={{ fontSize: '11.5px', color: 'var(--sub)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                              {tech.specialty}
                            </div>
                            <div style={{ fontSize: '10.5px', color: '#666', marginTop: '2px' }}>
                              {tech.experienceYears} năm KN · {tech.completedTicketsCount} ca xong
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                {/* SUBMIT BUTTON */}
                <div style={{ marginTop: '6px' }}>
                  <button
                    type="submit"
                    className="booking-submit-pill"
                    disabled={submitting}
                  >
                    {submitting ? 'Đang gửi yêu cầu...' : 'Gửi yêu cầu tiếp nhận máy'} <span>→</span>
                  </button>
                </div>

                <p className="booking-disclaimer" style={{ marginTop: '10px' }}>
                  Yêu cầu sẽ được gửi tức thì đến hệ thống TechCare. Kỹ thuật viên sẽ liên hệ xác nhận trong vòng 15 phút.
                </p>
              </div>

              {/* STEP CONNECTOR LINE */}
              <div className="stepper-connector-line">
                <span className="stepper-dot"></span>
              </div>

              {/* STEP 2: HARDWARE SPECIFICATIONS & ANTI-TAMPER SERIAL VERIFICATION */}
              <div className="booking-step-group step-2-group">
                <div className="step-header-row">
                  <div className="step-num-circle step-num-circle-secondary">2</div>
                  <div className="step-header-text">
                    <span className="step-title">Cấu hình & Serial linh kiện (Tùy chọn — Chống tráo đổi)</span>
                    <span className="step-subtitle">Quét tự động để lưu biên nhận hoặc bổ sung thông số bằng tay</span>
                  </div>
                </div>

                {/* Redesigned 1-Column Quick-Scan Card (Pure Monochrome) */}
                <div className="quick-scan-card-v2">
                  <div className="scan-card-header-bar">
                    <div className="scan-title-wrap">
                      <h4 className="scan-main-title">Quét tự động cấu hình & Serial linh kiện</h4>
                      <span className="scan-click-badge">1-CLICK</span>
                    </div>

                    <div className="scan-btn-group">
                      {hasHardwareData ? (
                        <button
                          type="button"
                          className="scan-btn-outline"
                          onClick={handleStartScan}
                          title="Tải lại script để quét cập nhật thông số mới"
                        >
                          Tải & Quét lại
                        </button>
                      ) : (
                        <>
                          <button
                            type="button"
                            className={`scan-btn-primary ${isListening ? 'listening' : ''}`}
                            onClick={handleStartScan}
                          >
                            {isListening ? 'Đang đợi máy đồng bộ...' : 'Tải script quét tự động (.bat)'}
                          </button>
                          <button
                            type="button"
                            className="scan-btn-secondary"
                            onClick={handleDownloadPs1}
                            title="Tải script dạng PowerShell .ps1"
                          >
                            .ps1
                          </button>
                        </>
                      )}
                    </div>
                  </div>

                  <p className="scan-lead-desc">
                    Tải script nhẹ và an toàn để máy tự trích xuất Serial máy, Serial ổ cứng SSD và từng thanh RAM vào đơn.
                  </p>

                  {/* Polling waiting indicator */}
                  {isListening && (
                    <div className="scan-waiting-banner">
                      <span className="pulse-dot"></span>
                      <span>Đang lắng nghe kết nối từ máy của bạn... Vui lòng mở và chạy file .bat vừa tải về.</span>
                    </div>
                  )}

                  {/* Verified Hardware Details Box */}
                  {hasHardwareData && (
                    <div className="scan-results-box">
                      {/* Top monochrome status bar */}
                      <div className="scan-verified-status-bar">
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                          <span style={{ fontSize: '13px' }}>✓</span>
                          <strong>ĐÃ XÁC THỰC CẤU HÌNH & SERIAL PHẦN CỨNG CHỐNG TRÁO ĐỔI</strong>
                        </div>
                        {scanTimestamp && (
                          <span className="scan-timestamp-tag">Quét lúc: {scanTimestamp}</span>
                        )}
                      </div>

                      {/* Technical Specs Structured List */}
                      <div className="specs-detail-list">
                        {/* Machine Serial & Model */}
                        <div className="spec-item-row">
                          <span className="spec-label">Serial máy (BIOS):</span>
                          <span className="spec-val">
                            {formData.serialNumber ? (
                              <code className="mono-serial-badge">{formData.serialNumber}</code>
                            ) : (
                              <span style={{ color: '#888' }}>(Đối chiếu tem thân máy)</span>
                            )}
                            {formData.modelName && <span style={{ marginLeft: '8px', fontWeight: 500 }}>— {formData.modelName}</span>}
                          </span>
                        </div>

                        {/* CPU */}
                        {formData.cpu && (
                          <div className="spec-item-row">
                            <span className="spec-label">Vi xử lý (CPU):</span>
                            <span className="spec-val">{formData.cpu}</span>
                          </div>
                        )}

                        {/* Disks & Individual Serials */}
                        {formData.diskSerials && formData.diskSerials.length > 0 ? (
                          <div className="spec-item-row">
                            <span className="spec-label">Ổ cứng SSD/HDD ({formData.diskSerials.length}):</span>
                            <div className="spec-val-column">
                              {formData.diskSerials.map((d, i) => (
                                <div key={i} className="component-serial-line">
                                  <span>Ổ {i + 1}: <strong>{d.sizeGB}GB {d.model}</strong></span>
                                  {d.serialNumber ? (
                                    <span style={{ marginLeft: '8px' }}>
                                      Serial: <code className="mono-serial-badge">{d.serialNumber}</code>
                                    </span>
                                  ) : (
                                    <span style={{ marginLeft: '8px', color: '#888', fontSize: '11.5px' }}>(Không có SN)</span>
                                  )}
                                </div>
                              ))}
                            </div>
                          </div>
                        ) : formData.storage ? (
                          <div className="spec-item-row">
                            <span className="spec-label">Ổ cứng:</span>
                            <span className="spec-val">{formData.storage}</span>
                          </div>
                        ) : null}

                        {/* RAM Modules & Individual Serials */}
                        {formData.ramModules && formData.ramModules.length > 0 ? (
                          <div className="spec-item-row">
                            <span className="spec-label">Bộ nhớ RAM ({formData.ramModules.length} thanh):</span>
                            <div className="spec-val-column">
                              {formData.ramModules.map((r, i) => (
                                <div key={i} className="component-serial-line">
                                  <span>Thanh {i + 1}: <strong>{r.capacityGB}GB {r.partNumber || ''}</strong></span>
                                  {r.serialNumber ? (
                                    <span style={{ marginLeft: '8px' }}>
                                      Serial: <code className="mono-serial-badge">{r.serialNumber}</code>
                                    </span>
                                  ) : (
                                    <span style={{ marginLeft: '8px', color: '#888', fontSize: '11.5px' }}>(Không có SN)</span>
                                  )}
                                </div>
                              ))}
                            </div>
                          </div>
                        ) : formData.ram ? (
                          <div className="spec-item-row">
                            <span className="spec-label">RAM:</span>
                            <span className="spec-val">{formData.ram}</span>
                          </div>
                        ) : null}

                        {/* OS */}
                        {formData.operatingSystem && (
                          <div className="spec-item-row">
                            <span className="spec-label">Hệ điều hành:</span>
                            <span className="spec-val">{formData.operatingSystem}</span>
                          </div>
                        )}
                      </div>
                    </div>
                  )}

                  {/* Toggle Manual Editing Drawer */}
                  <div className="scan-card-footer-toggle">
                    <button
                      type="button"
                      className="toggle-manual-btn"
                      onClick={() => setShowManualSpecs(!showManualSpecs)}
                    >
                      {showManualSpecs ? 'Thu gọn thông số kỹ thuật' : 'Hoặc tự điền / chỉnh sửa thông số máy thủ công'}
                    </button>
                  </div>
                </div>

                {/* Collapsible Manual Specs Input Fields */}
                {showManualSpecs && (
                  <div className="manual-specs-drawer" style={{ marginTop: '14px' }}>
                    <div className="form-row-2">
                      <div>
                        <label>Serial máy (BIOS / Tem thân máy)</label>
                        <input
                          type="text"
                          placeholder="Ví dụ: PF3XYZ12 hoặc 5CD12345"
                          className={autoFilled ? 'auto-filled-highlight' : ''}
                          value={formData.serialNumber}
                          onChange={(e) => setFormData({ ...formData, serialNumber: e.target.value })}
                        />
                      </div>
                      <div>
                        <label>Model máy</label>
                        <input
                          type="text"
                          placeholder="Ví dụ: ThinkPad X1 Carbon Gen 9"
                          className={autoFilled ? 'auto-filled-highlight' : ''}
                          value={formData.modelName}
                          onChange={(e) => setFormData({ ...formData, modelName: e.target.value })}
                        />
                      </div>
                    </div>

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
                          placeholder="Ví dụ: 512 GB NVMe SSD"
                          className={autoFilled ? 'auto-filled-highlight' : ''}
                          value={formData.storage}
                          onChange={(e) => setFormData({ ...formData, storage: e.target.value })}
                        />
                      </div>
                      <div>
                        <label>Hệ điều hành</label>
                        <input
                          type="text"
                          placeholder="Ví dụ: Windows 11 Pro 64-bit"
                          className={autoFilled ? 'auto-filled-highlight' : ''}
                          value={formData.operatingSystem}
                          onChange={(e) => setFormData({ ...formData, operatingSystem: e.target.value })}
                        />
                      </div>
                    </div>

                    <div className="form-row-2">
                      <div>
                        <label>Email liên hệ (Tùy chọn)</label>
                        <input
                          type="email"
                          placeholder="Ví dụ: an.nguyen@email.com"
                          value={formData.customerEmail}
                          onChange={(e) => setFormData({ ...formData, customerEmail: e.target.value })}
                        />
                      </div>
                    </div>
                  </div>
                )}
              </div>
            </form>
          )}
        </div>
      </div>
    </section>
  )
}
