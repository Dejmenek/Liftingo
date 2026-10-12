-- Revokes one session.
-- KEYS[1] = liftingo:session:{id}
-- ARGV[1] = key prefix, ARGV[2] = session id
-- Returns 1 when a session was revoked, 0 when it was already gone.
local prefix = ARGV[1]
local s = redis.call('HMGET', KEYS[1], 'userId', 'currentTokenHash')

if not s[1] then
  return 0
end

redis.call('DEL', KEYS[1])
if s[2] then
  redis.call('DEL', prefix .. 'rt:' .. s[2])
end
redis.call('SREM', prefix .. 'user-sessions:' .. s[1], ARGV[2])

return 1
