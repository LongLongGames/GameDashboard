import { useState } from 'react'
import { api, setToken } from '../api/client'

type Me = { userId: number; username: string; mustChangePassword: boolean; roles: string[] }

export default function Login({ onLogin }: { onLogin: (m: Me) => void }) {
  const [username, setUsername] = useState('root')
  const [password, setPassword] = useState('')
  const [err, setErr] = useState('')

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setErr('')
    try {
      const res = await api<Me & { token: string }>('/api/v1/auth/login', {
        method: 'POST',
        body: JSON.stringify({ username, password }),
      })
      setToken(res.token)
      onLogin(res)
    } catch (ex: unknown) {
      setErr(ex instanceof Error ? ex.message : '登录失败')
    }
  }

  return (
    <div className="login-box card">
      <h1>GameDashboard</h1>
      <p className="muted">公司级 GM / 运营 / 程序 聚合后台</p>
      <form onSubmit={submit}>
        <div className="form-row">
          <label>用户名</label>
          <input value={username} onChange={e => setUsername(e.target.value)} autoFocus />
        </div>
        <div className="form-row">
          <label>密码</label>
          <input type="password" value={password} onChange={e => setPassword(e.target.value)} />
        </div>
        {err && <p className="error">{err}</p>}
        <button type="submit" style={{ width: '100%' }}>登录</button>
      </form>
      <p className="muted" style={{ marginTop: '1rem' }}>默认 root / ChangeMe123! ，首次须改密</p>
    </div>
  )
}
