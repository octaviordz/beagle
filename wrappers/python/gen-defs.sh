#!/bin/sh

python `pkg-config pygtk-2.0 --variable=codegendir`/h2def.py \
../../libbeagle/beagle/*.h \
> beagle.defs.new

# Remove gtypes from the enums
./fix.pl beagle.defs.new > beagle.defs
rm beagle.defs.new

# Add the BeagleHit and BeagleTimestamp pointers
cat >> beagle.defs << EOF
;; Pointer types
(define-pointer Hit
  (in-module "Beagle")
  (c-name "BeagleHit")
  (gtype-id "BEAGLE_TYPE_HIT")    
)

(define-pointer Timestamp
  (in-module "Beagle")
  (c-name "BeagleTimestamp")
  (gtype-id "BEAGLE_TYPE_TIMESTAMP")    
)
EOF