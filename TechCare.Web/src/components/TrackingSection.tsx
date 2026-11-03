import React, { useState, useEffect, useCallback, useRef } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { API_BASE_URL } from '../api/client'

export interface ShipmentEventDto {
  occurredAtUtc: string
  status: string
  location?: string | null
  note?: string | null
}

export interface ShipmentTrackingDto {
  shipmentId: number
  appointmentId?: number | null
  repairTicketId?: number | null
  trackingCode: string
  provider: string
  status: string
  shippingFee: number
  printUrl?: string | null
  events: ShipmentEventDto[]
}

interface TicketInfo {
  code: string
  customerName: string
  phone: string
  modelName: string
  status: string
  scheduledAt: string
  issue: string
  shipment?: ShipmentTrackingDto | null
}

interface TrackingSectionProps {
  initialCode?: string
  initialPhone?: string
  autoSearch?: boolean
}

// Map Component with Leaflet
function ShipmentRouteMap({ shipment, customerLocation }: { shipment: ShipmentTrackingDto; customerLocation?: string }) {
  const mapContainerRef = useRef<HTMLDivElement>(null)
  const mapInstanceRef = useRef<L.Map | null>(null)

  useEffect(() => {
    if (!mapContainerRef.current) return

    // Avoid multiple instances on same div
    if (mapInstanceRef.current) {
      mapInstanceRef.current.remove()
      mapInstanceRef.current = null
    }

    // Default route points: Sender location -> GHN Sorting Hub -> TechCare Center
    const senderCoords: [number, number] = [21.0333, 105.7833] // Cầu Giấy, Hà Nội
    const hubCoords: [number, number] = [21.0125, 105.8010]    // GHN Hub Nam Từ Liêm / Thanh Xuân
    const techCareCoords: [number, number] = [21.0285, 105.8542] // TechCare Center Hoàn Kiếm

    const map = L.map(mapContainerRef.current, {
      zoomControl: true,
      attributionControl: false,
    }).setView([21.025, 105.815], 13)

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 18,
    }).addTo(map)

    const createIcon = (label: string, isDone: boolean) =>
      L.divIcon({
        className: 'custom-map-marker',
        html: `<div style="
          background: ${isDone ? '#111111' : '#ffffff'};
          color: ${isDone ? '#ffffff' : '#111111'};
          border: 2px solid #111111;
          border-radius: 999px;
          padding: 4px 10px;
          font-size: 11px;
          font-weight: 700;
          white-space: nowrap;
          box-shadow: 0 2px 8px rgba(0,0,0,0.18);
        ">${label}</div>`,
        iconSize: [80, 28],
        iconAnchor: [40, 14],
      })

    // Markers
    L.marker(senderCoords, { icon: createIcon('1. Bưu cục gửi', true) })
      .addTo(map)
      .bindPopup(`<b>Điểm gửi:</b> ${customerLocation || 'Địa chỉ khách hàng'}`)

    L.marker(hubCoords, {
      icon: createIcon('2. Kho GHN', shipment.status !== 'CREATED' && shipment.status !== 'PICKING'),
    })
      .addTo(map)
      .bindPopup(`<b>Kho phân loại:</b> Trung tâm chuyển phát GHN Express`)

    L.marker(techCareCoords, {
      icon: createIcon('3. TechCare', shipment.status === 'DELIVERED'),
    })
      .addTo(map)
      .bindPopup(`<b>Điểm nhận:</b> Xưởng kỹ thuật Trung tâm TechCare`)

    // Route Polyline
    const routeLine = L.polyline([senderCoords, hubCoords, techCareCoords], {
      color: '#111111',
      weight: 3,
      dashArray: '6, 8',
      opacity: 0.8,
    }).addTo(map)

    map.fitBounds(routeLine.getBounds(), { padding: [40, 40] })
    mapInstanceRef.current = map

    return () => {
      if (mapInstanceRef.current) {
        mapInstanceRef.current.remove()
        mapInstanceRef.current = null
      }
    }
  }, [shipment, customerLocation])

  return (
    <div style={{ marginTop: '16px', borderRadius: '10px', overflow: 'hidden', border: '1px solid var(--line)' }}>
      <div
        ref={mapContainerRef}
        style={{ width: '100%', height: '280px', background: '#f4f4f2' }}
      />
      <div style={{ padding: '8px 14px', background: '#fafaf8', borderTop: '1px solid var(--line)', fontSize: '11px', color: '#666', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <span>Bản đồ hành trình lộ trình bưu phẩm thời gian thực (Leaflet / OpenStreetMap)</span>
        <span style={{ fontWeight: 600, color: '#111' }}>Đối tác: {shipment.provider} Express</span>
      </div>
    </div>
  )
}

export function TrackingSection({ initialCode = '', initialPhone = '', autoSearch = false }: TrackingSectionProps) {
  const [code, setCode] = useState(initialCode)
  const [phone, setPhone] = useState(initialPhone)
  const [loading, setLoading] = useState(false)
  const [ticket, setTicket] = useState<TicketInfo | null>(null)
  const [searched, setSearched] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  const executeLookup = useCallback(async (searchCode: string, searchPhone: string) => {
    if (!searchCode.trim() || !searchPhone.trim()) {
      setErrorMessage('Vui lòng nhập cả Mã phiếu/đơn và Số điện thoại để tra cứu bảo mật.')
      setSearched(true)
      return
    }

    setLoading(true)
    setSearched(true)
    setTicket(null)
    setErrorMessage(null)

    try {
      const queryParams = new URLSearchParams({
        code: searchCode.trim(),
        phone: searchPhone.trim(),
      })
      const res = await fetch(`${API_BASE_URL}/api/v1/appointments/track?${queryParams.toString()}`)
      if (res.ok) {
        const result = await res.json()
        if (result && result.found && result.data) {
          const found = result.data
          const issueText = found.customerDescription || found.subject || found.customerIssue || 'Kiểm tra phần cứng tổng thể'
          const realShipment: ShipmentTrackingDto | null = result.shipment || null

          setTicket({
            code: found.appointmentCode || found.ticketCode || searchCode.trim().toUpperCase(),
            customerName: found.customerName || 'Khách hàng',
            phone: found.customerPhone || searchPhone.trim(),
            modelName: found.modelName || 'Laptop',
            status: found.status || 'BOOKED',
            scheduledAt: found.scheduledAtUtc ? new Date(found.scheduledAtUtc).toLocaleString('vi-VN') : 'Đang xử lý',
            issue: issueText,
            shipment: realShipment,
          })
        } else {
          setErrorMessage(result.message || 'Không tìm thấy thông tin đơn hẹn hoặc phiếu sửa chữa phù hợp.')
        }
      } else {
        const err = await res.json().catch(() => null)
        setErrorMessage(err?.message || 'Không thể tra cứu vào lúc này. Vui lòng kiểm tra lại thông tin.')
      }
    } catch {
      setErrorMessage('Lỗi kết nối đến máy chủ. Vui lòng kiểm tra lại mạng hoặc thử lại sau.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (initialCode) setCode(initialCode)
    if (initialPhone) setPhone(initialPhone)
    if (autoSearch && initialCode && initialPhone) {
      executeLookup(initialCode, initialPhone)
    }
  }, [initialCode, initialPhone, autoSearch, executeLookup])

  const handleLookup = (e: React.FormEvent) => {
    e.preventDefault()
    executeLookup(code, phone)
  }

  const standardSteps = [
    { key: 'BOOKED', label: '1. Đã đặt hẹn', desc: 'Tiếp nhận yêu cầu trên hệ thống' },
    { key: 'RECEIVED', label: '2. Đã nhận máy', desc: 'Lập biên bản ngoại quan tại quầy' },
    { key: 'DIAGNOSING', label: '3. Đang kiểm tra', desc: 'Kỹ sư đo đạc xác định nguyên nhân' },
    { key: 'REPAIRING', label: '4. Đang sửa chữa', desc: 'Khách đã duyệt giá, đang thay thế' },
    { key: 'READY', label: '5. Sẵn sàng bàn giao', desc: 'Đã test kiểm chuẩn và dán tem BH' },
  ]

  const getStepIndex = (status: string) => {
    const s = status.toUpperCase()
    if (s.includes('READY') || s.includes('DELIVERED') || s.includes('COMPLETED') || s.includes('RETURNED')) return 4
    if (s.includes('REPAIR')) return 3
    if (s.includes('DIAGNOS') || s.includes('WAITING')) return 2
    if (s.includes('RECEIV')) return 1
    return 0
  }

  const currentIdx = ticket ? getStepIndex(ticket.status) : 0

  const mapShipmentStatusText = (st: string) => {
    switch (st?.toUpperCase()) {
      case 'CREATED': return 'Đã tạo vận đơn trên hệ thống hãng'
      case 'PICKING': return 'Đang điều phối bưu tá đến lấy hàng'
      case 'PICKED': return 'Bưu tá đã lấy hàng thành công'
      case 'IN_TRANSIT': return 'Đang vận chuyển liên tỉnh qua trung tâm khai thác'
      case 'OUT_FOR_DELIVERY': return 'Đang giao hàng tới trung tâm TechCare'
      case 'DELIVERED': return 'Đã giao thành công tới trung tâm TechCare'
      case 'DELIVERY_FAILED': return 'Giao hàng không thành công'
      case 'RETURNING': return 'Đang chuyển hoàn'
      case 'RETURNED': return 'Đã hoàn trả'
      case 'CANCELLED': return 'Đã hủy vận đơn'
      case 'EXCEPTION': return 'Bưu kiện phát sinh sự cố kiểm tra'
      default: return st || 'Đang xử lý vận chuyển'
    }
  }

  return (
    <section id="tracking" className="tracking-section section-pad">
      <div className="section-heading">
        <div>
          <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '8px' }}>
            MINH BẠCH TIẾN TRÌNH & HÀNH TRÌNH
          </div>
          <h2>Tra cứu đơn sửa & Bảo hành.</h2>
        </div>
        <p className="section-intro">
          Nhập mã phiếu hẹn hoặc mã vận đơn cùng số điện thoại đăng ký của bạn để theo dõi tiến trình trực tiếp theo thời gian thực.
        </p>
      </div>

      <form className="tracking-search-bar" onSubmit={handleLookup} style={{ flexWrap: 'wrap' }}>
        <input
          type="text"
          placeholder="Mã hẹn / Mã phiếu (VD: APPT-123456, TC-2611...)"
          value={code}
          onChange={(e) => setCode(e.target.value)}
          style={{ minWidth: '240px' }}
        />
        <input
          type="tel"
          placeholder="Số điện thoại đăng ký (VD: 0901234567)"
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
          style={{ minWidth: '180px' }}
        />
        <button type="submit" className="button" disabled={loading}>
          {loading ? 'Đang tra cứu...' : 'Tra cứu tiến trình'} <span>→</span>
        </button>
      </form>

      {searched && ticket && (
        <div className="tracking-result-card">
          <div className="result-header">
            <div>
              <span className="badge-pill">Mã đơn: {ticket.code}</span>
              <h3 style={{ marginTop: '8px', fontSize: '18px', fontWeight: 600 }}>
                {ticket.modelName} — {ticket.customerName}
              </h3>
              <p style={{ color: '#7b7b7b', fontSize: '12.5px', marginTop: '4px' }}>
                Số điện thoại: {ticket.phone} | Thời gian: {ticket.scheduledAt}
              </p>
            </div>
            <div className="status-tag">
              <span className="status-dot"></span>
              <strong>{standardSteps[currentIdx]?.label || ticket.status}</strong>
            </div>
          </div>

          <div className="issue-box">
            <strong>Hiện trạng / Yêu cầu tiếp nhận:</strong> {ticket.issue}
          </div>

          {/* Standard In-Shop Repair Timeline */}
          <div style={{ marginBottom: '28px' }}>
            <h4 style={{ fontSize: '13px', textTransform: 'uppercase', letterSpacing: '0.04em', color: '#555', marginBottom: '14px' }}>
              Tiến trình kỹ thuật tại xưởng TechCare:
            </h4>
            <div className="timeline-track">
              {standardSteps.map((st, i) => (
                <div key={st.key} className={`timeline-step ${i <= currentIdx ? 'active' : ''}`}>
                  <div className="step-circle">{i <= currentIdx ? '✓' : i + 1}</div>
                  <div className="step-title">{st.label}</div>
                  <div className="step-desc">{st.desc}</div>
                </div>
              ))}
            </div>
          </div>

          {/* Real Courier Transport Route & Events (GHN Integration) */}
          {ticket.shipment ? (
            <div className="courier-timeline-section" style={{ marginTop: '24px', paddingTop: '20px', borderTop: '1px solid var(--line)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px', flexWrap: 'wrap', gap: '8px' }}>
                <div>
                  <h4 style={{ fontSize: '14px', fontWeight: 600, color: 'var(--ink)' }}>
                    Hành trình vận chuyển thực tế — {ticket.shipment.provider} Express
                  </h4>
                  <span style={{ fontSize: '12px', color: '#555' }}>
                    Mã vận đơn: <strong>{ticket.shipment.trackingCode}</strong> | Trạng thái: <strong>{mapShipmentStatusText(ticket.shipment.status)}</strong>
                    {ticket.shipment.shippingFee > 0 && ` | Cước vận chuyển: ${ticket.shipment.shippingFee.toLocaleString('vi-VN')}đ`}
                  </span>
                </div>
                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                  <span style={{ fontSize: '11px', background: '#111111', color: '#ffffff', padding: '4px 10px', borderRadius: '4px', fontWeight: 600 }}>
                    Bảo hiểm kiện hàng toàn phần
                  </span>
                  {ticket.shipment.printUrl && (
                    <a
                      href={ticket.shipment.printUrl}
                      target="_blank"
                      rel="noreferrer"
                      style={{ fontSize: '11px', border: '1px solid #111111', padding: '3px 8px', borderRadius: '4px', color: '#111111' }}
                    >
                      In vận đơn
                    </a>
                  )}
                </div>
              </div>

              {/* Leaflet Route Map */}
              <ShipmentRouteMap shipment={ticket.shipment} customerLocation={ticket.customerName} />

              {/* Chronological Real Events Timeline */}
              <div style={{ marginTop: '20px' }}>
                <h5 style={{ fontSize: '12.5px', fontWeight: 600, color: '#333', marginBottom: '10px', textTransform: 'uppercase', letterSpacing: '0.03em' }}>
                  Chi tiết sự kiện quét mã vận đơn (Thời gian thực):
                </h5>
                {ticket.shipment.events && ticket.shipment.events.length > 0 ? (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                    {ticket.shipment.events.map((ev, idx) => (
                      <div
                        key={idx}
                        style={{
                          display: 'grid',
                          gridTemplateColumns: '130px 1fr',
                          gap: '12px',
                          padding: '10px 14px',
                          background: idx === 0 ? '#ffffff' : '#fafaf8',
                          border: '1px solid ' + (idx === 0 ? '#111111' : '#e4e4e0'),
                          borderRadius: '8px',
                          fontSize: '12px',
                        }}
                      >
                        <div style={{ color: '#666', fontWeight: 500 }}>
                          {new Date(ev.occurredAtUtc).toLocaleString('vi-VN')}
                        </div>
                        <div>
                          <strong style={{ color: '#111' }}>{mapShipmentStatusText(ev.status)}</strong>
                          {ev.location && <span style={{ color: '#555', marginLeft: '6px' }}>— {ev.location}</span>}
                          {ev.note && <div style={{ color: '#777', fontSize: '11.5px', marginTop: '2px' }}>{ev.note}</div>}
                        </div>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div style={{ padding: '14px', background: '#fafaf8', border: '1px solid #e4e4e0', borderRadius: '8px', fontSize: '12px', color: '#666' }}>
                    Vận đơn đã khởi tạo trên hệ thống {ticket.shipment.provider} Express. Bưu tá đang trên đường đến lấy thiết bị và cập nhật sự kiện quét mã đầu tiên.
                  </div>
                )}
              </div>
            </div>
          ) : null}
        </div>
      )}

      {searched && !ticket && !loading && (
        <div className="no-result-card">
          <p>Không tìm thấy phiếu sửa chữa hoặc đơn hẹn khớp với thông tin đã nhập.</p>
          <span style={{ fontSize: '12.5px', color: '#c53030', display: 'block', marginTop: '6px' }}>
            {errorMessage || 'Vui lòng kiểm tra lại chính xác Mã phiếu và Số điện thoại đã đăng ký.'}
          </span>
        </div>
      )}
    </section>
  )
}
