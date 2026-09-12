import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'

type Game = { id: number; gameId: string; displayName: string; baseUrl: string }

export default function Home({ me }: { me: { username: string; roles: string[] } }) {
  const [games, setGames] = useState<Game[]>([])
  useEffect(() => { api<Game[]>('/api/v1/games').then(setGames).catch(() => {}) }, [])

  return (
    <>
      <h1>欢迎，{me.username}</h1>
      <p className="muted">角色：{me.roles.join(', ')}</p>
      <div className="card">
        <h2>已启用游戏</h2>
        <ul>
          {games.map(g => (
            <li key={g.gameId}><strong>{g.displayName}</strong> <span className="muted">({g.gameId})</span>
              <br /><code className="muted">{g.baseUrl}</code></li>
          ))}
        </ul>
      </div>
      <div className="card">
        <Link to="/match3">Match3 闭环 →</Link>
      </div>
    </>
  )
}
