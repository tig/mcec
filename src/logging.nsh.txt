!define LogSet "!insertmacro LogSetMacro"
!macro LogSetMacro SETTING
  !ifdef ENABLE_LOGGING
    LogSet ${SETTING}
  !endif
!macroend
 
!define LogText "!insertmacro LogTextMacro"
!macro LogTextMacro INPUT_TEXT
  !ifdef ENABLE_LOGGING
    LogText ${INPUT_TEXT}
  !endif
!macroend