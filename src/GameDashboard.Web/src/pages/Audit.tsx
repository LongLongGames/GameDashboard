import { useEffect, useState } from 'react'
import { api } from '../api/client'

type R = { id: number; username: string; action: string; gameId?: string; target?: string; detail?: string; success: boolean; createdAt: string }

export default function Audit() {
  const [list, setList] = useState<R[]>([])
  useEffect(() => { api<R[]>('/api/v1/audit').then(setList).catch(() => {}) }, [])
  return (
    <>
      <h1>审计</h1>
      <div className="card">
        <table>
          <thead><tr><th>时间</th><th>用户</th><th>动作</th><th>游戏</th><th>目标</th><th>结果</th></tr></thead>
          <tbody>
            {list.map(a => (
              <tr key={a.id}>
                <td>{a.createdAt}</td><td>{a.username}</td><td>{a.action}</td>
                <td>{a.gameId}</td><td>{a.target}</td>
                <td><span className={`badge ${a.success ? 'ok' : 'fail'}`}>{a.success ? 'OK' : 'FAIL'}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}
