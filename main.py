def OCCURS(cSearchExpression, cExpressionSearched):
  if cSearchExpression.count(cExpressionSearched) == 0:
    return 0
  else:
    occurances = cSearchExpression.count(cExpressionSearched)
    return occurances

def SUBSTR(cExpression, cStart, nCharactersReturned):
  start = int(cStart)
  if start > len(cExpression):
    print("Error, start position is greater than the length of the expression. Are you sure your using an expression?")
  elif nCharactersReturned is None or " ":
    return cExpression[:len(cExpression)]
  else:
    val = cExpression[:nCharactersReturned]
    return val

def AT(cSearchExpression, cExpressionSearched, nOccurrence):
  if nOccurrence > cSearchExpression.count(cExpressionSearched):
    return "Error, there are not that many occurances of the expression."
  else:
    return cSearchExpression.find(cExpressionSearched)

def VAL(cExpression):
  first_letter = cExpression[:1]
  length = len(cExpression)
  if first_letter.isnumeric() or "-" or "+":
      if length < 16:
        return int(cExpression)
  if length > 16:
    rounded = round(int(cExpression[:16]))
    return rounded
  else:
    return 0

print(VAL("12345756895693452353723423423423423423423568945677857954654675878976"))

print(AT("Hello World", "l", 2))

print(SUBSTR("Hello World", 1, " "))

print(OCCURS("Hello World", "Hello"))