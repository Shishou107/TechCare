import React, { useState, useEffect } from 'react'
import { API_BASE_URL } from '../../api/client'

interface KnowledgeDoc {
  documentId: number
  title: string
  category: string
  status: string
  chunkCount: number
  createdAtUtc: string
}

export function KnowledgeAdmin() {
  const [docs, setDocs] = useState<KnowledgeDoc[]>([])
  const [loading, setLoading] = useState(false)
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState({ title: '', category: 'BẢNG GIÁ', rawContent: '' })
  const [submitting, setSubmitting] = useState(false)
  const [msg, setMsg] = useState<{ text: string; error?: boolean } | null>(null)

  const token = localStorage.getItem('tc_token')

  const fetchDocs = async () => {
    setLoading(true)
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/admin/knowledge/documents`, {
        headers: { Authorization: `Bearer ${token}` },
      })
      if (res.ok) {
        const data = await res.json()
        setDocs(data)
      }
    } catch (e) {
      console.error(e)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchDocs()
  }, [])

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!form.title.trim() || !form.rawContent.trim()) {
      setMsg({ text: 'Vui lòng điền đủ Tiêu đề và Nội dung tài liệu.', error: true })
      return
    }

    setSubmitting(true)
    setMsg(null)
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/admin/knowledge/documents`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify(form),
      })
      if (res.ok) {
        setMsg({ text: 'Nạp tài liệu & phân mảnh Vector thành công!' })
        setForm({ title: '', category: 'BẢNG GIÁ', rawContent: '' })
        setShowForm(false)
        fetchDocs()
      } else {
        const err = await res.json().catch(() => null)
        setMsg({ text: err?.message || 'Lỗi khi nạp tài liệu.', error: true })
      }
    } catch {
      setMsg({ text: 'Lỗi kết nối máy chủ.', error: true })
    } finally {
      setSubmitting(false)
    }
  }

  const handleDelete = async (id: number) => {
    if (!window.confirm('Bạn có chắc muốn xóa tài liệu này khỏi cơ sở tri thức?')) return
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/admin/knowledge/documents/${id}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` },
      })
      if (res.ok) {
        fetchDocs()
      }
    } catch (e) {
      console.error(e)
    }
  }

  return (
    <div style={{ maxWidth: '1200px', margin: '0 auto', padding: '32px 24px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <div className="eyebrow">QUẢN TRỊ TRI THỨC AI (RAG)</div>
          <h2 style={{ fontSize: '26px', fontWeight: 600, color: 'var(--ink)' }}>Cơ sở dữ liệu Tri thức Chatbot</h2>
          <p style={{ color: 'var(--sub)', fontSize: '13.5px', marginTop: '4px' }}>
            Tài liệu nạp vào đây sẽ tự động phân mảnh (~600 từ, trượt 80 từ) và sinh Vector Embedding phục vụ trả lời khách hàng.
          </p>
        </div>
        <button
          type="button"
          className="button"
          onClick={() => setShowForm(!showForm)}
        >
          {showForm ? 'Đóng biểu mẫu' : '+ Thêm tài liệu mới'}
        </button>
      </div>

      {msg && (
        <div style={{ padding: '10px 14px', borderRadius: '8px', marginBottom: '16px', background: msg.error ? '#fef2f2' : '#f4f4f2', color: msg.error ? '#dc2626' : '#111', fontSize: '13px' }}>
          {msg.text}
        </div>
      )}

      {showForm && (
        <form onSubmit={handleCreate} style={{ background: '#ffffff', border: '1.5px solid #111111', borderRadius: '12px', padding: '24px', marginBottom: '32px' }}>
          <h3 style={{ fontSize: '16px', fontWeight: 600, marginBottom: '16px' }}>Nạp tài liệu tri thức mới</h3>
          
          <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '16px', marginBottom: '14px' }}>
            <div>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '6px' }}>Tiêu đề tài liệu *</label>
              <input
                type="text"
                placeholder="Ví dụ: Bảng giá thay thế linh kiện màn hình Dell, Asus 2026"
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
                style={{ width: '100%', padding: '10px 12px', border: '1px solid #d4d4d0', borderRadius: '8px', fontSize: '13px' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '6px' }}>Chuyên mục *</label>
              <select
                value={form.category}
                onChange={(e) => setForm({ ...form, category: e.target.value })}
                style={{ width: '100%', padding: '10px 12px', border: '1px solid #d4d4d0', borderRadius: '8px', fontSize: '13px', background: '#fff' }}
              >
                <option value="BẢNG GIÁ">BẢNG GIÁ LINH KIỆN</option>
                <option value="QUY TRÌNH">QUY TRÌNH & CHÍNH SÁCH</option>
                <option value="BẢO HÀNH">CHÍNH SÁCH BẢO HÀNH</option>
                <option value="HỎI ĐÁP">HỎI ĐÁP KỸ THUẬT</option>
                <option value="VẬN CHUYỂN">GIAO NHẬN & VẬN CHUYỂN</option>
              </select>
            </div>
          </div>

          <div style={{ marginBottom: '16px' }}>
            <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '6px' }}>Nội dung tài liệu chi tiết (Hệ thống sẽ tự động phân mảnh theo ngữ nghĩa) *</label>
            <textarea
              rows={8}
              placeholder="Dán toàn bộ nội dung chính sách, bảng giá hoặc hướng dẫn vào đây..."
              value={form.rawContent}
              onChange={(e) => setForm({ ...form, rawContent: e.target.value })}
              style={{ width: '100%', padding: '10px 12px', border: '1px solid #d4d4d0', borderRadius: '8px', fontSize: '13px' }}
            />
          </div>

          <div style={{ display: 'flex', gap: '10px' }}>
            <button type="submit" className="button" disabled={submitting}>
              {submitting ? 'Đang phân tích & sinh Vector...' : 'Lưu & Khởi tạo Vector'}
            </button>
            <button type="button" onClick={() => setShowForm(false)} style={{ background: '#fff', border: '1px solid #d4d4d0', padding: '10px 18px', borderRadius: '999px', fontSize: '13px', cursor: 'pointer' }}>
              Hủy
            </button>
          </div>
        </form>
      )}

      {/* Documents List */}
      <div style={{ background: '#ffffff', border: '1px solid var(--line)', borderRadius: '12px', overflow: 'hidden' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
          <thead>
            <tr style={{ background: '#fafaf8', borderBottom: '1px solid var(--line)', color: '#666' }}>
              <th style={{ padding: '12px 16px' }}>ID</th>
              <th style={{ padding: '12px 16px' }}>Tiêu đề tài liệu</th>
              <th style={{ padding: '12px 16px' }}>Chuyên mục</th>
              <th style={{ padding: '12px 16px' }}>Số phân mảnh (Chunks)</th>
              <th style={{ padding: '12px 16px' }}>Trạng thái</th>
              <th style={{ padding: '12px 16px' }}>Ngày nạp</th>
              <th style={{ padding: '12px 16px', textAlign: 'right' }}>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={7} style={{ padding: '24px', textAlign: 'center', color: '#777' }}>
                  Đang tải danh sách tri thức...
                </td>
              </tr>
            ) : docs.length === 0 ? (
              <tr>
                <td colSpan={7} style={{ padding: '32px', textAlign: 'center', color: '#888' }}>
                  Chưa có tài liệu tri thức nào. Bấm "+ Thêm tài liệu mới" để nạp văn bản đầu tiên.
                </td>
              </tr>
            ) : (
              docs.map((d) => (
                <tr key={d.documentId} style={{ borderBottom: '1px solid #f0f0ee' }}>
                  <td style={{ padding: '12px 16px', color: '#888', fontFamily: 'monospace' }}>#{d.documentId}</td>
                  <td style={{ padding: '12px 16px', fontWeight: 600, color: 'var(--ink)' }}>{d.title}</td>
                  <td style={{ padding: '12px 16px' }}>
                    <span style={{ background: '#f4f4f2', border: '1px solid #e4e4e0', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>
                      {d.category}
                    </span>
                  </td>
                  <td style={{ padding: '12px 16px' }}>
                    <strong>{d.chunkCount}</strong> mảnh vector
                  </td>
                  <td style={{ padding: '12px 16px' }}>
                    <span style={{ background: '#111111', color: '#ffffff', padding: '2px 8px', borderRadius: '4px', fontSize: '10.5px', fontWeight: 600 }}>
                      {d.status}
                    </span>
                  </td>
                  <td style={{ padding: '12px 16px', color: '#666' }}>
                    {new Date(d.createdAtUtc).toLocaleDateString('vi-VN')}
                  </td>
                  <td style={{ padding: '12px 16px', textAlign: 'right' }}>
                    <button
                      type="button"
                      onClick={() => handleDelete(d.documentId)}
                      style={{ background: 'transparent', border: '1px solid #dc2626', color: '#dc2626', padding: '3px 10px', borderRadius: '4px', fontSize: '11.5px', cursor: 'pointer' }}
                    >
                      Xóa
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}

