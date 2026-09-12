import { useEffect, useState } from 'react'
import { api } from '../api/client'

type B = { id: string; title: string; status: string; gameId?: string; reporter?: string; createdAt: string; detail?: string }

export default function Bugs() {
  const [list, setList] = useState<B[]>([])
  useEffect(() => { api<B[]>('/api/v1/bugs?gameId=match3').then(setList).catch(() => {}) }, [])
  return (
    <>
      <h1>Bug</h1>
      <div className="card">
        <table>
          <thead><tr><th>ID</th><th>标题</th><th>状态</th><th>游戏</th></tr></thead>
          <tbody>
            {list.map(b => (
              <tr key={b.id}><td>{b.id}</td><td>{b.title}<br /><span className="muted">{b.detail}</span></td><td>{b.status}</td><td>{b.gameId}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}
