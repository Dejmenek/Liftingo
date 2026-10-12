-- Revokes the session a refresh token belongs to (logout), whatever the token's state.
-- KEYS[1] = liftingo:rt:{hash}
-- ARGV[1] = key prefix
-- Returns {} when the token matches no session, otherwise {userId, deviceId, deviceType, sessionId}.
local prefix = ARGV[1]
local sessionId = redis.call('HGET', KEYS[1], 'sessionId')

if not sessionId then
  return {}
end

local sessionKey = prefix .. 'session:' .. sessionId
local s = redis.call('HMGET', sessionKey, 'userId', 'deviceId', 'deviceType', 'currentTokenHash')

if not s[1] then
  return {}
end

redis.call('DEL', sessionKey)
if s[4] then
  redis.call('DEL', prefix .. 'rt:' .. s[4])
end
redis.call('SREM', prefix .. 'user-sessions:' .. s[1], sessionId)

return {s[1], s[2], s[3], sessionId}
