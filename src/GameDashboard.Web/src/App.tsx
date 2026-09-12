import { Routes, Route, Navigate, Link, useNavigate } from 'react-router-dom'
import { useEffect, useState } from 'react'
import { api, getToken, setToken } from './api/client'
import Login from './pages/Login'
import ChangePassword from './pages/ChangePassword'
import Home from './pages/Home'
import Match3 from './pages/Match3'
import Players from './pages/Players'
import Users from './pages/Users'
import Audit from './pages/Audit'
import Bugs from './pages/Bugs'
import Mail from './pages/Mail'

type Me = { userId: number; username: string; mustChangePassword: boolean; roles: string[] }

export default function App() {
  const [me, setMe] = useState<Me | null>(null)
  const [loading, setLoading] = useState(!!getToken())
  const nav = useNavigate()

  useEffect(() => {
    if (!getToken()) { setLoading(false); return }
    api<Me>('/api/v1/auth/me')
      .then(setMe)
      .catch(() => setMe(null))
      .finally(() => setLoading(false))
  }, [])

  async function logout() {
    try { await api('/api/v1/auth/logout', { method: 'POST' }) } catch { /* */ }
    setToken(null)
    setMe(null)
    nav('/login')
  }

  if (loading) return <div className="container muted">加载中…</div>

  if (!me) {
    return (
      <Routes>
        <Route path="/login" element={<Login onLogin={setMe} />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    )
  }

  if (me.mustChangePassword) {
    return (
      <Routes>
        <Route path="/change-password" element={<ChangePassword onDone={() => setMe({ ...me, mustChangePassword: false })} />} />
        <Route path="*" element={<Navigate to="/change-password" replace />} />
      </Routes>
    )
  }

  const isAdmin = me.roles.includes('SuperAdmin')

  return (
    <>
      <nav className="nav">
        <span className="brand">GameDashboard</span>
        <Link to="/">首页</Link>
        <Link to="/match3">Match3</Link>
        <Link to="/players">玩家</Link>
        <Link to="/mail">邮件</Link>
        <Link to="/bugs">Bug</Link>
        <Link to="/audit">审计</Link>
        {isAdmin && <Link to="/users">账号</Link>}
        <span className="muted" style={{ marginLeft: 'auto' }}>{me.username}</span>
        <button type="button" className="secondary" onClick={logout}>退出</button>
      </nav>
      <div className="container">
        <Routes>
          <Route path="/" element={<Home me={me} />} />
          <Route path="/match3" element={<Match3 />} />
          <Route path="/players" element={<Players />} />
          <Route path="/mail" element={<Mail />} />
          <Route path="/bugs" element={<Bugs />} />
          <Route path="/audit" element={<Audit />} />
          {isAdmin && <Route path="/users" element={<Users />} />}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </div>
    </>
  )
}
