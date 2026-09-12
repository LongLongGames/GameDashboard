import { useEffect, useState } from 'react'
import { api } from '../api/client'

type U = { id: number; username: string; mustChangePassword: boolean; isActive: boolean; roles: string[]; games: string[] }

export default function Users() {
  const [list, setList] = useState<U[]>([])
  const [username, setU] = useState('')
  const [password, setP] = useState('')
  const [roles, setRoles] = useState<string[]>(['GM'])
  const [msg, setMsg] = useState('')

  const load = () => api<U[]>('/api/v1/users').then(setList)
  useEffect(() => { load().catch(e => setMsg(e.message)) }, [])

  async function create(e: React.FormEvent) {
    e.preventDefault()
    try {
      await api('/api/v1/users', {
        method: 'POST',
        body: JSON.stringify({ username, password, roleNames: roles, gameIds: ['match3'] }),
      })
      setMsg('已创建')
      setU(''); setP('')
      load()
    } catch (ex: unknown) {
      setMsg(ex instanceof Error ? ex.message : '失败')
    }
  }

  function toggleRole(r: string) {
    setRoles(prev => prev.includes(r) ? prev.filter(x => x !== r) : [...prev, r])
  }

  return (
    <>
      <h1>账号</h1>
      {msg && <p className="muted">{msg}</p>}
      <form className="card" onSubmit={create}>
        <h2>创建</h2>
        <div className="form-row"><label>用户名</label><input value={username} onChange={e => setU(e.target.value)} /></div>
        <div className="form-row"><label>初始密码</label><input type="password" value={password} onChange={e => setP(e.target.value)} /></div>
        <div className="form-row">
          <label>角色</label>
          <div style={{ display: 'flex', gap: 12 }}>
            {['SuperAdmin', 'GM', 'Operator', 'Developer'].map(r => (
              <label key={r}><input type="checkbox" checked={roles.includes(r)} onChange={() => toggleRole(r)} /> {r}</label>
            ))}
          </div>
        </div>
        <button type="submit">创建</button>
      </form>
      <div className="card">
        <table>
          <thead><tr><th>ID</th><th>用户</th><th>角色</th><th>游戏</th><th>状态</th></tr></thead>
          <tbody>
            {list.map(u => (
              <tr key={u.id}>
                <td>{u.id}</td>
                <td>{u.username}{u.mustChangePassword ? ' (改密)' : ''}</td>
                <td>{u.roles?.join(', ')}</td>
                <td>{u.games?.join(', ')}</td>
                <td>{u.isActive ? '启用' : '禁用'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}
