//////////////////////////////////////////////////////////////////////	
// sample gestures using prepose language
//////////////////////////////////////////////////////////////////////

APP sample:
    GESTURE wave : 
        POSE on_right :
            put your right wrist above your right elbow,
            put your right wrist to the right of your right elbow.
	
        POSE on_left : 
            put your right wrist above your right elbow,
            put your right wrist to the left of your right elbow.
	
        EXECUTION : 
            on_right,
            on_left,
            on_right,
            on_left.
    
     GESTURE clap : 
        POSE a_part :
            don't touch your right hand with your left hand.
	
        POSE together : 
           	touch your right hand with your left hand.
            
        EXECUTION : 
            a_part,
            together.
            
     GESTURE crossover-arm-stretch:
		POSE relax-arms:
		point your arms down.
		
		POSE stretch:
		rotate your left arm 90 degrees to your right, 
		touch your left elbow with your right hand.
		
		EXECUTION:
		relax-arms,
		slowly stretch and hold for 30 seconds.
