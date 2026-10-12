-- Revokes every session of a user.
-- KEYS[1] = liftingo:user-sessions:{userId}
-- ARGV[1] = key prefix
-- Returns how many live sessions were revoked. Ids of sessions that had already expired are dropped.
local prefix = ARGV[1]
local ids = redis.call('SMEMBERS', KEYS[1])
local revoked = 0

for _, id in ipairs(ids) do
  local sessionKey = prefix .. 'session:' .. id
  local currentTokenHash = redis.call('HGET', sessionKey, 'currentTokenHash')

  if redis.call('DEL', sessionKey) == 1 then
    revoked = revoked + 1
  end
  if currentTokenHash then
    redis.call('DEL', prefix .. 'rt:' .. currentTokenHash)
  end
end

redis.call('DEL', KEYS[1])

return revoked
