import { useState } from 'react'
import { api } from '../api/client'

export default function ChangePassword({ onDone }: { onDone: () => void }) {
  const [currentPassword, setCur] = useState('')
  const [newPassword, setNew] = useState('')
  const [confirm, setConfirm] = useState('')
  const [err, setErr] = useState('')

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    if (newPassword !== confirm) { setErr('两次新密码不一致'); return }
    try {
      await api('/api/v1/auth/change-password', {
        method: 'POST',
        body: JSON.stringify({ currentPassword, newPassword }),
      })
      onDone()
    } catch (ex: unknown) {
      setErr(ex instanceof Error ? ex.message : '失败')
    }
  }

  return (
    <div className="login-box card">
      <h1>修改密码</h1>
      <p className="muted">首次登录或重置后必须改密</p>
      <form onSubmit={submit}>
        <div className="form-row"><label>当前密码</label>
          <input type="password" value={currentPassword} onChange={e => setCur(e.target.value)} /></div>
        <div className="form-row"><label>新密码（≥8）</label>
          <input type="password" value={newPassword} onChange={e => setNew(e.target.value)} /></div>
        <div className="form-row"><label>确认</label>
          <input type="password" value={confirm} onChange={e => setConfirm(e.target.value)} /></div>
        {err && <p className="error">{err}</p>}
        <button type="submit" style={{ width: '100%' }}>保存</button>
      </form>
    </div>
  )
}
