-- Presents a refresh token (R4 steps 1-4) in one atomic step.
-- KEYS[1] = liftingo:rt:{presented hash}
-- ARGV[1] = key prefix ("liftingo:")
-- ARGV[2] = Base64Url hash of the new refresh token
-- ARGV[3] = now, Unix milliseconds
-- ARGV[4] = inactivity timeout, milliseconds
-- ARGV[5] = now, ISO 8601
-- Returns {0} invalid, {1, userId, deviceId, deviceType, sessionId, ttlMs} rotated, {2, userId, deviceId, deviceType, sessionId} reused.
local prefix = ARGV[1]
local rt = redis.call('HMGET', KEYS[1], 'sessionId', 'state')
local sessionId = rt[1]
local state = rt[2]

if not sessionId then
  return {0}
end

local sessionKey = prefix .. 'session:' .. sessionId
local s = redis.call('HMGET', sessionKey, 'userId', 'deviceId', 'deviceType', 'absoluteExpiresAt', 'currentTokenHash')
local userId = s[1]
local deviceId = s[2]
local deviceType = s[3]
local absoluteExpiresAt = tonumber(s[4])
local currentTokenHash = s[5]

if not userId then
  return {0}
end

local function revoke()
  redis.call('DEL', sessionKey)
  if currentTokenHash then
    redis.call('DEL', prefix .. 'rt:' .. currentTokenHash)
  end
  redis.call('SREM', prefix .. 'user-sessions:' .. userId, sessionId)
end

if state == 'used' then
  revoke()
  return {2, userId, deviceId, deviceType, sessionId}
end

local now = tonumber(ARGV[3])
local ttl = math.min(tonumber(ARGV[4]), absoluteExpiresAt - now)

if ttl <= 0 then
  -- Past the absolute lifetime: the session is over however recently it was renewed.
  revoke()
  return {0}
end

local newRtKey = prefix .. 'rt:' .. ARGV[2]
redis.call('HSET', KEYS[1], 'state', 'used')
redis.call('HSET', newRtKey, 'sessionId', sessionId, 'state', 'active')
redis.call('HSET', sessionKey, 'currentTokenHash', ARGV[2], 'lastRenewedAt', ARGV[5])

redis.call('PEXPIRE', sessionKey, ttl)
redis.call('PEXPIRE', KEYS[1], ttl)
redis.call('PEXPIRE', newRtKey, ttl)
redis.call('PEXPIRE', prefix .. 'user-sessions:' .. userId, tonumber(ARGV[4]))

return {1, userId, deviceId, deviceType, sessionId, ttl}
